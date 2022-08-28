const apiVersion = 'v3.0'

import express from 'express'
import * as utils from '../utils/requestUtils.js'
export var router = express.Router()

import _ from 'lodash'

// Routes

router.get('/:year/schedule/:eventCode/:tournamentLevel', async (req, res) => {
    var response = await utils.GetDataFromFIRST(`${req.params.year}/schedule/${req.params.eventCode}/${req.params.tournamentLevel}`)
    res.header('cache-control', response.headers['cache-control'])
    res.json(response.body)
})

router.get('/:year/matches/:eventCode/:tournamentLevel', async (req, res) => {
    var response = await utils.GetDataFromFIRST(`${req.params.year}/matches/${req.params.eventCode}/${req.params.tournamentLevel}`)
    res.header('cache-control', response.headers['cache-control'])
    res.json(response.body)
})

router.get('/:year/schedule/hybrid/:eventCode/:tournamentLevel', async (req, res) => {
    const schedule = await BuildHybridSchedule(req.params.year, req.params.eventCode, req.params.tournamentLevel)
    res.json({
        Schedule: schedule
    })
})

router.get('/:year/awards/event/:eventCode', async (req, res) => {
    var response = await utils.GetDataFromFIRST(`${req.params.year}/awards/event/${req.params.eventCode}`)
    res.header('cache-control', response.headers['cache-control'])
    res.json(response.body)
})

router.get('/:year/events', async (req, res) => {
    var response = await utils.GetDataFromFIRST(`${req.params.year}/events`)
    res.header('cache-control', response.headers['cache-control'])
    res.json(response.body)
})


// Helper functions

const BuildHybridSchedule = async (year, eventCode, tournamentLevel) => {
    const scheduleResponse = await utils.GetDataFromFIRST(`${year}/schedule/${eventCode}/${tournamentLevel}`)
    let matchesResponse
    try {
        matchesResponse = await utils.GetDataFromFIRST(`${year}/matches/${eventCode}/${tournamentLevel}`)
    } catch (e) {
        return scheduleResponse.body.Schedule
    }
    const schedule = scheduleResponse.body.Schedule
    const matches = matchesResponse.body.Matches

    _.merge(schedule, matches)

    return schedule
}