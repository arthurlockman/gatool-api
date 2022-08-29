import 'dotenv/config'

import express from 'express'
import morgan from 'morgan'
import { auth } from 'express-oauth2-jwt-bearer'

import { router as v3Router } from './routes/v3-endpoints.js'
import { ReadSecret } from './utils/secretUtils.js'
import { GetUserPreferences } from './utils/storageUtils.js'

var app = express()

app.get('/liveCheck', (_, res) => {
  res.json({ message: `I'm alive!` })
})

app.use(auth({
  issuerBaseURL: await ReadSecret('Auth0Issuer'),
  audience: await ReadSecret('Auth0Audience'),

}))
app.use(morgan('dev'))
app.use(express.json({ limit: '50mb' }))
app.use(express.urlencoded({ limit: '50mb', extended: true, parameterLimit: 50000 }))

app.use((_, res, next) => {
  res.append('Access-Control-Allow-Origin', ['*'])
  res.append('Access-Control-Allow-Methods', 'GET,PUT,POST,DELETE')
  res.append('Access-Control-Allow-Headers', 'Content-Type')
  next()
})

app.use('/v3', v3Router)

const port = process.env.PORT ?? 3000;
app.listen(port, () => console.log(`gatool running on port ${port}`))