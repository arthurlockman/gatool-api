import 'dotenv/config'

import newrelic from 'newrelic'

import express from 'express'
import pinoHTTP from 'pino-http'
import logger from './logger.js'
import morgan from 'morgan'
import { auth } from 'express-oauth2-jwt-bearer'
import { unless } from 'express-unless'
import 'express-async-errors'

import { router as v3Router } from './routes/v3-endpoints.js'
import { ReadSecret } from './utils/secretUtils.js'

var app = express()
app.use(pinoHTTP({logger}))

var auth0 = auth({
  issuerBaseURL: await ReadSecret('Auth0Issuer'),
  audience: await ReadSecret('Auth0Audience'),

})
auth0.unless = unless

app.options("/*", function(_req, res, next){
  res.header('Access-Control-Allow-Origin', '*');
  res.header('Access-Control-Allow-Methods', 'GET,PUT,POST,DELETE,OPTIONS');
  res.header('Access-Control-Allow-Headers', 'Content-Type, Authorization, Content-Length, X-Requested-With');
  res.send(200);
});

app.use(auth0.unless({
  path:[
    '/livecheck',
    '/version',
    /.*avatar\.png/,
    '/v3/admin/updateHighScores'
  ]
}))

import * as fs from 'fs'
var appVersion
try {
  appVersion = fs.readFileSync('version.txt', 'utf8').replace('\n', '')
} catch {
  appVersion = 'UNKNOWN'
}

app.use(morgan('dev'))
app.use(express.json({ limit: '50mb' }))
app.use(express.urlencoded({ limit: '50mb', extended: true, parameterLimit: 50000 }))

app.get('/livecheck', (_, res) => {
  res.setHeader('Cache-Control', 'no-cache')
  res.json({ message: `I'm alive!` })
})

app.get('/version', (_, res) => {
  res.setHeader('Cache-Control', 'no-cache')
  res.json({ sha: appVersion })
})

app.use((_, res, next) => {
  res.append('Access-Control-Allow-Origin', ['*'])
  res.append('Access-Control-Allow-Methods', 'GET,PUT,POST,DELETE')
  res.append('Access-Control-Allow-Headers', 'Content-Type')
  next()
})

app.use((err, _req, _res, next) => {
  logger.error(err)
  next(err)
})

app.use('/v3', v3Router)

// Catch unhandled exceptions
app.use(function(err, req, res, next) {
  res.status(err?.statusCode || err?.response?.statusCode || 500)
  res.json({ error: err.message })
  next(err)
});

newrelic.instrumentLoadedModule(
  'express',
  app
);

const port = process.env.PORT ?? 3000;
app.listen(port, () => logger.info(`gatool running on port ${port}`))