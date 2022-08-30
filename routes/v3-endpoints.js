import express from 'express'
import * as requestUtils from '../utils/requestUtils.js'
import * as scoreUtils from '../utils/scoreUtils.js'
export var router = express.Router()

import _ from 'lodash'
import {
    GetTeamUpdates, GetUserPreferences, StoreTeamUpdates,
    StoreUserPreferences, StoreHighScores, GetHighScores
} from '../utils/storageUtils.js'
import { ReadSecret } from '../utils/secretUtils.js'

// Routes

router.get('/:year/schedule/:eventCode/:tournamentLevel', async (req, res) => {
    var response = await requestUtils.GetDataFromFIRST(`${req.params.year}/schedule/${req.params.eventCode}/${req.params.tournamentLevel}`)
    res.header('cache-control', response.headers['cache-control'])
    res.json(response.body)
})

router.get('/:year/matches/:eventCode/:tournamentLevel', async (req, res) => {
    var response = await requestUtils.GetDataFromFIRST(`${req.params.year}/matches/${req.params.eventCode}/${req.params.tournamentLevel}`)
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
    var response = await requestUtils.GetDataFromFIRST(`${req.params.year}/awards/event/${req.params.eventCode}`)
    res.header('cache-control', response.headers['cache-control'])
    res.json(response.body)
})

router.get('/:year/events', async (req, res) => {
    var response = await requestUtils.GetDataFromFIRST(`${req.params.year}/events`)
    res.header('cache-control', response.headers['cache-control'])
    res.json(response.body)
})

router.get('/:year/scores/:eventCode/:tournamentLevel/:start/:end', async (req, res) => {
    let response = undefined
    if (req.params.start === req.params.end) {
        response = await requestUtils.GetDataFromFIRST(`${req.params.year}/scores/${req.params.eventCode}/${req.params.tournamentLevel}?matchNumber=${req.params.start}`)
    } else {
        response = await requestUtils.GetDataFromFIRST(`${req.params.year}/scores/${req.params.eventCode}/${req.params.tournamentLevel}?start=${req.params.start}&end=${req.params.end}`)
    }
    res.header('cache-control', response.headers['cache-control'])
    res.json(response.body)
})

router.get('/team/:teamNumber/updates', async (req, res) => {
    try {
        var updates = await GetTeamUpdates(req.params.teamNumber)
        res.json(JSON.parse(updates))
    } catch (e) {
        console.error(e)
        res.status(404).send(`No updates found for team ${req.params.teamNumber}`)
    }
})

router.put('/team/:teamNumber/updates', async (req, res) => {
    await StoreTeamUpdates(req.params.teamNumber, req.body)
    res.status(204).send()
})

router.get('/:year/awards/team/:teamNumber', async (req, res) => {
    var response = await requestUtils.GetDataFromFIRST(`${req.params.year}/awards/team/${req.params.teamNumber}`)
    res.header('cache-control', response.headers['cache-control'])
    res.json(response.body)
})

router.get('/:year/avatars/team/:teamNumber/avatar.png', async (req, res) => {
    try {
        const avatar = await requestUtils.GetDataFromFIRST(
            `${req.params.year}/avatars?teamNumber=${req.params.teamNumber}`)
        const teamAvatar = avatar.body.teams[0]
        if (teamAvatar.encodedAvatar == null) {
            res.status(404)
            res.json({ message: 'Avatar not found' })
        }
        res.setHeader('Content-Type', 'image/png')
        res.setHeader('Charset', 'utf-8')
        res.send(Buffer.from(teamAvatar.encodedAvatar, 'base64'))
    } catch (e) {
        const statusCode = e?.response?.statusCode ? parseInt(e.response.statusCode, 10) : 404
        const message = e?.response?.body ? e.response.body : 'Avatar not found.'
        res.status(statusCode)
        res.json({ message: message })
    }
})

router.get('/:year/rankings/:eventCode', async (req, res) => {
    var response = await requestUtils.GetDataFromFIRST(`${req.params.year}/rankings/${req.params.eventCode}`)
    res.header('cache-control', response.headers['cache-control'])
    res.json(response.body)
})

router.get('/:year/district/rankings/:districtCode', async (req, res) => {
    const query = []
    query.push(`districtCode=${req.params.districtCode}`)
    if (req.query) {
        const top = req.query.top
        if (top) {
            query.push(`top=${top}`)
        }
    }
    const rankingData = await requestUtils.GetDataFromFIRST(`${req.params.year}/rankings/district?${query.join('&')}&page=1`)
    if (rankingData.body.statusCode) {
        res.header('cache-control', rankingData.headers['cache-control'])
        res.status(rankingData.body.statusCode).json(rankingData.body.message)
        res.header('cache-control', rankingData.headers['cache-control'])
        res.json(rankingData.body)
    }
    if (rankingData.body.pageTotal === 1) {
        res.header('cache-control', rankingData.headers['cache-control'])
        res.json(rankingData.body)
    } else {
        const promises = []
        for (let i = 2; i <= rankingData.body.pageTotal; i++) {
            promises.push(requestUtils.GetDataFromFIRST(`${req.params.year}/rankings/district?${query.join('&')}&page=${i}`))
        }
        const allRankData = await Promise.all(promises)
        allRankData.map(districtRank => {
            rankingData.body.rankingCountPage += districtRank.body.rankingCountPage
            rankingData.body.districtRanks = rankingData.body.districtRanks.concat(districtRank.body.districtRanks)
        })
        rankingData.body.pageTotal = 1
        res.header('cache-control', rankingData.headers['cache-control'])
        res.json(rankingData.body)
    }
})

router.get('/:year/highscores/:eventCode', async (req, res) => {
    const eventList = await requestUtils.GetDataFromFIRST(`${req.params.year}/events/`)
    const evtList = eventList.body.Events.filter(evt => evt.code === req.params.eventCode)
    if (evtList.length !== 1) {
        res.status(404).send('Event not found')
    }
    const eventDetails = evtList[0]
    const qualMatchList = await BuildHybridSchedule(req.params.year, req.params.eventCode, 'qual')
    const playoffMatchList = await BuildHybridSchedule(req.params.year, req.params.eventCode, 'playoff')

    let matches = qualMatchList
        .map(x => { return { event: { eventCode: eventDetails.code, type: 'qual' }, match: x } })
        .concat(playoffMatchList
            .map(x => { return { event: { eventCode: eventDetails.code, type: 'playoff' }, match: x } }))
    matches = matches.filter(match => match.match.postResultTime && match.match.postResultTime !== '' &&
        // TODO: find a better way to filter these demo teams out, this way is not sustainable
        match.match.teams.filter(t => t.teamNumber >= 9986).length === 0)

    const overallHighScorePlayoff = []
    const overallHighScoreQual = []
    const penaltyFreeHighScorePlayoff = []
    const penaltyFreeHighScoreQual = []
    const offsettingPenaltyHighScorePlayoff = []
    const offsettingPenaltyHighScoreQual = []
    for (const match of matches) {
        if (match.event.type === 'playoff') {
            overallHighScorePlayoff.push(match)
        }
        if (match.event.type === 'qual') {
            overallHighScoreQual.push(match)
        }
        if (match.event.type === 'playoff'
            && match.match.scoreBlueFoul === 0 && match.match.scoreRedFoul === 0) {
            penaltyFreeHighScorePlayoff.push(match)
        } else if (match.event.type === 'qual'
            && match.match.scoreBlueFoul === 0 && match.match.scoreRedFoul === 0) {
            penaltyFreeHighScoreQual.push(match)
        } else if (match.event.type === 'playoff'
            && match.match.scoreBlueFoul === match.match.scoreRedFoul && match.match.scoreBlueFoul > 0) {
            offsettingPenaltyHighScorePlayoff.push(match)
        } else if (match.event.type === 'qual'
            && match.match.scoreBlueFoul === match.match.scoreRedFoul && match.match.scoreBlueFoul > 0) {
            offsettingPenaltyHighScoreQual.push(match)
        }
    }
    const highScoresData = []
    if (overallHighScorePlayoff.length > 0) {
        highScoresData.push(scoreUtils.BuildHighScoreJson(req.params.year, 'overall', 'playoff',
            scoreUtils.FindHighestScore(overallHighScorePlayoff)))
    }
    if (overallHighScoreQual.length > 0) {
        highScoresData.push(scoreUtils.BuildHighScoreJson(req.params.year, 'overall', 'qual',
            scoreUtils.FindHighestScore(overallHighScoreQual)))
    }
    if (penaltyFreeHighScorePlayoff.length > 0) {
        highScoresData.push(scoreUtils.BuildHighScoreJson(req.params.year, 'penaltyFree', 'playoff',
            scoreUtils.FindHighestScore(penaltyFreeHighScorePlayoff)))
    }
    if (penaltyFreeHighScoreQual.length > 0) {
        highScoresData.push(scoreUtils.BuildHighScoreJson(req.params.year, 'penaltyFree', 'qual',
            scoreUtils.FindHighestScore(penaltyFreeHighScoreQual)))
    }
    if (offsettingPenaltyHighScorePlayoff.length > 0) {
        highScoresData.push(scoreUtils.BuildHighScoreJson(req.params.year, 'offsetting', 'playoff',
            scoreUtils.FindHighestScore(offsettingPenaltyHighScorePlayoff)))
    }
    if (offsettingPenaltyHighScoreQual.length > 0) {
        highScoresData.push(scoreUtils.BuildHighScoreJson(req.params.year, 'offsetting', 'qual',
            scoreUtils.FindHighestScore(offsettingPenaltyHighScoreQual)))
    }
    res.json(highScoresData)
})

router.get('/:year/highscores', async (req, res) => {
    var scores = await GetHighScores(req.params.year)
    res.json(scores)
})

router.get('/admin/updateHighScores', async (_, res) => {
    const frcCurrentSeason = await ReadSecret('FRCCurrentSeason')
    const eventList = await requestUtils.GetDataFromFIRST(`${frcCurrentSeason}/events`)
    const promises = []
    const order = []
    const currentDate = new Date()
    currentDate.setDate(currentDate.getDate() + 1)
    for (const _event of eventList.body.Events) {
        const eventDate = new Date(_event.dateStart)
        if (eventDate < currentDate) {
            promises.push(BuildHybridSchedule(frcCurrentSeason, _event.code, 'qual').catch(_ => {
                return []
            }))
            promises.push(BuildHybridSchedule(frcCurrentSeason, _event.code, 'playoff').catch(_ => {
                return []
            }))
            order.push({
                eventCode: _event.code,
                type: 'qual'
            })
            order.push({
                eventCode: _event.code,
                type: 'playoff'
            })
        }
    }
    const events = await Promise.all(promises)
    const matches = []
    for (const _event of events) {
        const evt = order[events.indexOf(_event)]
        if (_event.length > 0) {
            for (const match of _event) {
                // TODO: find a better way to filter these demo teams out, this way is not sustainable
                if (match.postResultTime && match.postResultTime !== '' && match.teams.filter(t => t.teamNumber >= 9986).length === 0) {
                    // Result was posted and it's not a demo team, so the match has occurred
                    matches.push({
                        event: evt,
                        match: match
                    })
                }
            }
        } else {
            console.log('Event', evt.eventCode, evt.type, 'has no schedule data, likely occurs in the future')
        }
    }
    const overallHighScorePlayoff = []
    const overallHighScoreQual = []
    const penaltyFreeHighScorePlayoff = []
    const penaltyFreeHighScoreQual = []
    const offsettingPenaltyHighScorePlayoff = []
    const offsettingPenaltyHighScoreQual = []
    for (const match of matches) {
        if (match.event.type === 'playoff') {
            overallHighScorePlayoff.push(match)
        }
        if (match.event.type === 'qual') {
            overallHighScoreQual.push(match)
        }
        if (match.event.type === 'playoff'
            && match.match.scoreBlueFoul === 0 && match.match.scoreRedFoul === 0) {
            penaltyFreeHighScorePlayoff.push(match)
        } else if (match.event.type === 'qual'
            && match.match.scoreBlueFoul === 0 && match.match.scoreRedFoul === 0) {
            penaltyFreeHighScoreQual.push(match)
        } else if (match.event.type === 'playoff'
            && match.match.scoreBlueFoul === match.match.scoreRedFoul && match.match.scoreBlueFoul > 0) {
            offsettingPenaltyHighScorePlayoff.push(match)
        } else if (match.event.type === 'qual'
            && match.match.scoreBlueFoul === match.match.scoreRedFoul && match.match.scoreBlueFoul > 0) {
            offsettingPenaltyHighScoreQual.push(match)
        }
    }
    const highScorePromises = []
    highScorePromises.push(StoreHighScores(frcCurrentSeason, 'overall', 'playoff',
        scoreUtils.FindHighestScore(overallHighScorePlayoff)))
    highScorePromises.push(StoreHighScores(frcCurrentSeason, 'overall', 'qual',
        scoreUtils.FindHighestScore(overallHighScoreQual)))
    highScorePromises.push(StoreHighScores(frcCurrentSeason, 'penaltyFree', 'playoff',
        scoreUtils.FindHighestScore(penaltyFreeHighScorePlayoff)))
    highScorePromises.push(StoreHighScores(frcCurrentSeason, 'penaltyFree', 'qual',
        scoreUtils.FindHighestScore(penaltyFreeHighScoreQual)))
    highScorePromises.push(StoreHighScores(frcCurrentSeason, 'offsetting', 'playoff',
        scoreUtils.FindHighestScore(offsettingPenaltyHighScorePlayoff)))
    highScorePromises.push(StoreHighScores(frcCurrentSeason, 'offsetting', 'qual',
        scoreUtils.FindHighestScore(offsettingPenaltyHighScoreQual)))
    await Promise.all(highScorePromises)

    res.status(204).send()
})

// User Data Storage

router.get('/user/preferences', async (req, res) => {
    try {
        var email = req.auth.payload.email
        var prefs = await GetUserPreferences(email)
        res.json(JSON.parse(prefs))
    } catch (e) {
        console.error(e)
        res.status(404).send()
    }
})

router.put('/user/preferences', async (req, res) => {
    var email = req.auth.payload.email
    await StoreUserPreferences(email, req.body)
    res.status(204).send()
})


// Helper functions

const BuildHybridSchedule = async (year, eventCode, tournamentLevel) => {
    const scheduleResponse = await requestUtils.GetDataFromFIRST(`${year}/schedule/${eventCode}/${tournamentLevel}`)
    let matchesResponse
    try {
        matchesResponse = await requestUtils.GetDataFromFIRST(`${year}/matches/${eventCode}/${tournamentLevel}`)
    } catch (e) {
        return scheduleResponse.body.Schedule
    }
    const schedule = scheduleResponse.body.Schedule
    const matches = matchesResponse.body.Matches

    _.merge(schedule, matches)

    return schedule
}