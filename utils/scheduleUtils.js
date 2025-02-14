import * as requestUtils from "./requestUtils.js";
import _ from "lodash";

export const BuildHybridSchedule = async (year, eventCode, tournamentLevel) => {
    const scheduleResponse = await requestUtils.GetDataFromFIRST(`${year}/schedule/${eventCode}/${tournamentLevel}`)
    let matchesResponse
    try {
        matchesResponse = await requestUtils.GetDataFromFIRST(`${year}/matches/${eventCode}/${tournamentLevel}`)
    } catch (e) {
        return scheduleResponse.body.Schedule || scheduleResponse.body.schedule
    }
    const schedule = scheduleResponse.body.schedule || scheduleResponse.body.Schedule
    const matches = matchesResponse.body.matches || matchesResponse.body.Matches
    const headers = {schedule: scheduleResponse.headers, matches: matchesResponse.headers}

    _.merge(schedule, matches)
    return {schedule: schedule, headers: headers}
}