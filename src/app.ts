import 'dotenv/config';

import newrelic from 'newrelic';

import express, { NextFunction, Request, Response } from 'express';
import pinoHTTP from 'pino-http';
import logger from './logger';
import { auth } from 'express-oauth2-jwt-bearer';
import { unless } from 'express-unless';
import 'express-async-errors';

import { router as v3Router } from './routes/v3-endpoints';
import { router as systemRouter } from './routes/systemEndpoints';
import { ReadSecret } from './utils/secretUtils';

import * as os from 'os';

import * as fs from 'fs';
import { UpdateHighScores } from './utils/update-high-scores';
import { SyncUsers } from './utils/syncUsers';
import * as inspector from 'inspector';

import cron from 'node-cron';

const hostname = os.hostname();

function isInDebugMode() {
  return inspector.url() !== undefined;
}

// If we're running on the A host, run the timer.
if (hostname.toLocaleLowerCase() === 'gatool-worker' || isInDebugMode()) {
  logger.info(`Running on ${hostname}, starting background timers.`);
  cron.schedule('*/15 * * * *', async () => {
    logger.info('Updating high scores...');
    await UpdateHighScores();
    logger.info('Done updating high scores.');
  });
  cron.schedule('0 */6 * * *', async () => {
    logger.info('Starting user sync...');
    await SyncUsers();
    logger.info('User sync complete.');
  });
}

const app = express();

const pino = pinoHTTP({
  logger,
  redact: ['req.headers.authorization']
});
// @ts-expect-error yeah i know it's not there i'm adding it
pino.unless = unless;
app.use(
  // @ts-expect-error yeah i know it's not there i'm adding it
  pino.unless({
    path: ['/livecheck', '/version']
  })
);

const auth0 = auth({
  issuerBaseURL: await ReadSecret('Auth0Issuer'),
  audience: await ReadSecret('Auth0Audience')
});
// @ts-expect-error yeah i know it's not there i'm adding it
auth0.unless = unless;

// noinspection JSUnusedLocalSymbols
app.options('/*', (_req, res) => {
  res.header('Access-Control-Allow-Origin', '*');
  res.header('Access-Control-Allow-Methods', 'GET,PUT,POST,DELETE,OPTIONS');
  res.header('Access-Control-Allow-Headers', 'Content-Type, Authorization, Content-Length, X-Requested-With');
  res.send(200);
});

app.use(
  // @ts-expect-error yeah i know it's not there I'm adding it
  auth0.unless({
    path: ['/livecheck', '/version', /.*avatar\.png/, '/v3/admin/updateHighScores']
  })
);

let appVersion;
try {
  appVersion = fs.readFileSync('version.txt', 'utf8').replace('\n', '');
} catch {
  appVersion = 'UNKNOWN';
}

app.use(express.json({ limit: '50mb' }));
app.use(express.urlencoded({ limit: '50mb', extended: true, parameterLimit: 50000 }));

app.get('/livecheck', (_, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  res.json({ message: `I'm alive!` });
});

app.get('/version', (_, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  res.json({ sha: appVersion });
});

app.use((_, res, next) => {
  res.append('Access-Control-Allow-Origin', ['*']);
  res.append('Access-Control-Allow-Methods', 'GET,PUT,POST,DELETE');
  res.append('Access-Control-Allow-Headers', 'Content-Type');
  next();
});

app.use((err: any, _1: Request, _2: Response, next: NextFunction) => {
  logger.error(err);
  next(err);
});

app.use('/v3', v3Router);
app.use('/v3/system', systemRouter);

// Catch unhandled exceptions
app.use((err: any, _: Request, res: Response, next: NextFunction) => {
  res.status(err?.statusCode || err?.response?.statusCode || 500);
  if (err?.request?.requestUrl) {
    const message = `Received error "${err.message}" from upstream ${err.request.options.method} ${err.request.requestUrl}`;
    logger.error(err, message);
    res.json({ error: message });
  } else {
    logger.error(err);
    res.json({ error: err.message });
  }
  next(err);
});

newrelic.instrumentLoadedModule('express', app);

const port = process.env.PORT ?? 3001;
app.listen(port, () => {
  logger.info(`gatool running on port ${port}`);
});
