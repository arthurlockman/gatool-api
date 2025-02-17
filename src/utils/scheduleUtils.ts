import * as requestUtils from './requestUtils';
import _ from 'lodash';

export interface HybridSchedule {
  schedule: HybridMatch[];
  headers?: any;
}

export const BuildHybridSchedule = async (
  year: number,
  eventCode: string,
  tournamentLevel: string
): Promise<HybridSchedule> => {
  const scheduleResponse = await requestUtils.GetDataFromFIRST<ScheduleResponse>(
    `${year}/schedule/${eventCode}/${tournamentLevel}`
  );
  let matchesResponse;
  try {
    matchesResponse = await requestUtils.GetDataFromFIRST<MatchResponse>(
      `${year}/matches/${eventCode}/${tournamentLevel}`
    );
  } catch (_) {
    return { schedule: (scheduleResponse.body.Schedule || scheduleResponse.body.schedule) as HybridMatch[] };
  }
  const schedule = scheduleResponse.body.schedule || scheduleResponse.body.Schedule;
  const matches = matchesResponse.body.matches || matchesResponse.body.Matches;
  const headers = { schedule: scheduleResponse.headers, matches: matchesResponse.headers };

  _.merge(schedule, matches);
  return { schedule: schedule as HybridMatch[], headers };
};
