import 'dotenv/config'

import express from 'express'
import morgan from 'morgan'
import { auth } from 'express-oauth2-jwt-bearer'
import { unless } from 'express-unless'
import 'express-async-errors'
import cron from 'node-cron'
import { performance } from 'perf_hooks'

import { router as v3Router, UpdateHighScores } from './routes/v3-endpoints.js'
import { ReadSecret } from './utils/secretUtils.js'
import { AcquireHighScoresLock, ReleaseHighScoresLock } from './utils/storageUtils.js'

var app = express()
var auth0 = auth({
  issuerBaseURL: await ReadSecret('Auth0Issuer'),
  audience: await ReadSecret('Auth0Audience'),

})
auth0.unless = unless

app.use(auth0.unless({
  path:[
    '/livecheck',
    /.*avatar\.png/
  ]
}))

app.use(morgan('dev'))
app.use(express.json({ limit: '50mb' }))
app.use(express.urlencoded({ limit: '50mb', extended: true, parameterLimit: 50000 }))

app.get('/livecheck', (_, res) => {
  res.json({ message: `I'm alive!` })
})

app.use((_, res, next) => {
  res.append('Access-Control-Allow-Origin', ['*'])
  res.append('Access-Control-Allow-Methods', 'GET,PUT,POST,DELETE')
  res.append('Access-Control-Allow-Headers', 'Content-Type')
  next()
})

app.use('/v3', v3Router)

// Catch unhandled exceptions
app.use(function(err, req, res, next) {
  res.status(err?.statusCode || err?.response?.statusCode || 500)
  res.json({ error: err.message })
  next(err)
});

// Refresh high scores regularly
cron.schedule('*/10 * * * *', async () => {
  if (await AcquireHighScoresLock()) {
    console.log('Updating global high scores...')
    const start = performance.now()
    await UpdateHighScores()
    const time = (performance.now() - start) / 1000
    console.log(`Updated global high scores in ${time} seconds. Releasing lock...`)
    await ReleaseHighScoresLock()
  } else {
    console.log('Skipping high score update, could not get lock.')
  }
})

const port = process.env.PORT ?? 3000;
app.listen(port, () => console.log(`gatool running on port ${port}`))