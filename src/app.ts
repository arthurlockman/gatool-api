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
import { router as userRouter } from './routes/userEndpoints';
import { router as announcementsRouter } from './routes/announcementsEndpoints';
import { ReadSecret } from './utils/secretUtils';
import * as fs from 'fs';

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

// noinspection JSUnusedLocalSymbols
app.options('/*', (_req, res) => {
  res.header('Access-Control-Allow-Origin', '*');
  res.header('Access-Control-Allow-Methods', 'GET,PUT,POST,DELETE,OPTIONS');
  res.header('Access-Control-Allow-Headers', 'Content-Type, Authorization, Content-Length, X-Requested-With');
  res.send(200);
});

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
app.use('/v3/announcements', announcementsRouter);

// Authenticated routes below here
app.use(auth0);
app.use('/v3/user', userRouter);
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
