import * as requestUtils from "./requestUtils.js";
import logger from "../logger.js";
import {StoreHighScores} from "./storageUtils.js";
import * as scoreUtils from "./scoreUtils.js";
import {ReadSecret} from "./secretUtils.js";
import {BuildHybridSchedule} from "./scheduleUtils.js";

const frcCurrentSeason = await ReadSecret('FRCCurrentSeason')

export const UpdateHighScores = async () => {
    const eventList = await requestUtils.GetDataFromFIRST(`${frcCurrentSeason}/events`)
    const promises = []
    const order = []
    const currentDate = new Date()
    currentDate.setDate(currentDate.getDate() + 1)
    logger.info(`Found ${eventList.body.Events.length} events for ${frcCurrentSeason}`)
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
    logger.info(`Retrieved data for ${events.length} events`)
    for (const _event of events) {
        const evt = order[events.indexOf(_event)]
        if (_event.schedule.length > 0) {
            for (const match of _event.schedule) {
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
            logger.info(`Event ${evt.eventCode}, ${evt.type} has no schedule data, likely occurs in the future`)
        }
    }
    const overallHighScorePlayoff = []
    const overallHighScoreQual = []
    const penaltyFreeHighScorePlayoff = []
    const penaltyFreeHighScoreQual = []
    const offsettingPenaltyHighScorePlayoff = []
    const offsettingPenaltyHighScoreQual = []
    logger.info(`Found ${matches.length} total matches with data`)
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
}