import * as requestUtils from './requestUtils';
import logger from '../logger';
import { StoreHighScores } from './storageUtils';
import * as scoreUtils from './scoreUtils';
import { ReadSecret } from './secretUtils';
import { BuildHybridSchedule } from './scheduleUtils';
import PQueue from 'p-queue';

const frcCurrentSeason = +(await ReadSecret('FRCCurrentSeason'));

export const UpdateHighScores = async () => {
  const eventList = await requestUtils.GetDataFromFIRST<EventListResponse>(`${frcCurrentSeason}/events`);
  const districts = await requestUtils.GetDataFromFIRST<DistrictListResponse>(`${frcCurrentSeason}/districts`);
  const events: any = [];
  const currentDate = new Date();
  currentDate.setDate(currentDate.getDate() + 1);
  logger.info(`Found ${eventList.body.Events.length} events for ${frcCurrentSeason}`);
  const eventDataQueue = new PQueue({
    concurrency: 50
  });
  eventDataQueue.on('completed', result => {
    events.push(result);
  })
  for (const _event of eventList.body.Events) {
    const eventDate = new Date(_event.dateStart);
    if (eventDate < currentDate) {
      // noinspection ES6MissingAwait
      eventDataQueue.add(async () => {
        return {
          metadata: {
            eventCode: _event.code,
            districtCode: _event.districtCode,
            type: 'qual'
          },
          schedule: await BuildHybridSchedule(frcCurrentSeason, _event.code, 'qual').catch((_) => {
            return null;
          })
        };
      });
      // noinspection ES6MissingAwait
      eventDataQueue.add(async () => {
        return {metadata: {
            eventCode: _event.code,
            districtCode: _event.districtCode,
            type: 'playoff'
          }, schedule: await BuildHybridSchedule(frcCurrentSeason, _event.code, 'playoff').catch((_) => {
            return null;
          })};
      })
    }
  }
  await eventDataQueue.onIdle();
  const matches = [];
  logger.info(`Retrieved data for ${events.length} events`);
  for (const _event of events) {
    if (!!_event && !!_event.schedule && _event.schedule.schedule.length > 0) {
      for (const match of _event.schedule.schedule) {
        // TODO: find a better way to filter these demo teams out, this way is not sustainable
        // FIRST says teams >=9970 and <=9999 are offseason demo teams
        if (
          match.postResultTime &&
          match.postResultTime !== '' &&
          match.teams.filter((t: any) => t.teamNumber >= 9986 && t.teamNumber <= 9999).length === 0
        ) {
          // Result was posted, and it's not all demo teams, so the match has occurred
          matches.push({
            event: _event.metadata,
            match
          });
        }
      }
    } else {
      logger.info(`Event ${_event.metadata.eventCode}, ${_event.metadata.type} has no schedule data, likely occurs in the future`);
    }
  }
  const overallHighScorePlayoff = [];
  const overallHighScoreQual = [];
  const penaltyFreeHighScorePlayoff = [];
  const penaltyFreeHighScoreQual = [];
  const TBAPenaltyFreeHighScorePlayoff = [];
  const TBAPenaltyFreeHighScoreQual = [];
  const offsettingPenaltyHighScorePlayoff = [];
  const offsettingPenaltyHighScoreQual = [];
  logger.info(`Found ${matches.length} total matches with data`);
  for (const match of matches) {
    if (match.event.type === 'playoff') {
      // Load match results into overall playoff bucket
      overallHighScorePlayoff.push(match);
    }
    if (match.event.type === 'qual') {
      // Load match results into overall quals bucket
      overallHighScoreQual.push(match);
    }
    if (
      match.event.type === 'playoff' &&
      ((match.match.scoreBlueFoul === 0 && match.match.scoreBlueFinal >= match.match.scoreRedFinal) ||
        (match.match.scoreRedFoul === 0 && match.match.scoreBlueFinal <= match.match.scoreRedFinal))
    ) {
      // Load match results into TBA playoff bucket because the winning Alliance had no fouls
      TBAPenaltyFreeHighScorePlayoff.push(match);
    }
    if (
      match.event.type === 'qual' &&
      ((match.match.scoreBlueFoul === 0 && match.match.scoreBlueFinal >= match.match.scoreRedFinal) ||
        (match.match.scoreRedFoul === 0 && match.match.scoreBlueFinal <= match.match.scoreRedFinal))
    ) {
      // Load match results into TBA playoff bucket because the winning Alliance had no fouls
      TBAPenaltyFreeHighScoreQual.push(match);
    }
    if (match.event.type === 'playoff' && match.match.scoreBlueFoul === 0 && match.match.scoreRedFoul === 0) {
      // Load match results into penalty free playoff bucket
      penaltyFreeHighScorePlayoff.push(match);
    } else if (match.event.type === 'qual' && match.match.scoreBlueFoul === 0 && match.match.scoreRedFoul === 0) {
      // Load match results into penalty free playoff bucket
      penaltyFreeHighScoreQual.push(match);
    } else if (
      match.event.type === 'playoff' &&
      match.match.scoreBlueFoul === match.match.scoreRedFoul &&
      match.match.scoreBlueFoul > 0
    ) {
      // Load match results into offsetting fouls playoff bucket
      offsettingPenaltyHighScorePlayoff.push(match);
    } else if (
      match.event.type === 'qual' &&
      match.match.scoreBlueFoul === match.match.scoreRedFoul &&
      match.match.scoreBlueFoul > 0
    ) {
      // Load match results into offsetting fouls playoff bucket
      offsettingPenaltyHighScoreQual.push(match);
    }
  }

  await StoreHighScores(frcCurrentSeason, 'overall', 'playoff', scoreUtils.FindHighestScore(overallHighScorePlayoff));
  await StoreHighScores(frcCurrentSeason, 'overall', 'qual', scoreUtils.FindHighestScore(overallHighScoreQual));
  await StoreHighScores(
    frcCurrentSeason,
    'TBAPenaltyFree',
    'playoff',
    scoreUtils.FindHighestScore(TBAPenaltyFreeHighScorePlayoff)
  );
  await StoreHighScores(
    frcCurrentSeason,
    'TBAPenaltyFree',
    'qual',
    scoreUtils.FindHighestScore(TBAPenaltyFreeHighScoreQual)
  );
  await StoreHighScores(
    frcCurrentSeason,
    'penaltyFree',
    'playoff',
    scoreUtils.FindHighestScore(penaltyFreeHighScorePlayoff)
  );
  await StoreHighScores(frcCurrentSeason, 'penaltyFree', 'qual', scoreUtils.FindHighestScore(penaltyFreeHighScoreQual));
  await StoreHighScores(
    frcCurrentSeason,
    'offsetting',
    'playoff',
    scoreUtils.FindHighestScore(offsettingPenaltyHighScorePlayoff)
  );
  await StoreHighScores(
    frcCurrentSeason,
    'offsetting',
    'qual',
    scoreUtils.FindHighestScore(offsettingPenaltyHighScoreQual)
  );

  // Calculate high scores for each district
  for (const district of districts.body.districts) {
    await StoreHighScores(
      frcCurrentSeason,
      `District${district.code}Overall`,
      'playoff',
      scoreUtils.FindHighestScore(overallHighScorePlayoff.filter((m) => m.event?.districtCode == district.code))
    );
    await StoreHighScores(
      frcCurrentSeason,
      `District${district.code}Overall`,
      'qual',
      scoreUtils.FindHighestScore(overallHighScoreQual.filter((m) => m.event?.districtCode == district.code))
    );
    await StoreHighScores(
      frcCurrentSeason,
      `District${district.code}offsetting`,
      'playoff',
      scoreUtils.FindHighestScore(
        offsettingPenaltyHighScorePlayoff.filter((m) => m.event?.districtCode == district.code)
      )
    );
    await StoreHighScores(
      frcCurrentSeason,
      `District${district.code}offsetting`,
      'qual',
      scoreUtils.FindHighestScore(offsettingPenaltyHighScoreQual.filter((m) => m.event?.districtCode == district.code))
    );
    await StoreHighScores(
      frcCurrentSeason,
      `District${district.code}PenaltyFree`,
      'playoff',
      scoreUtils.FindHighestScore(penaltyFreeHighScorePlayoff.filter((m) => m.event?.districtCode == district.code))
    );
    await StoreHighScores(
      frcCurrentSeason,
      `District${district.code}PenaltyFree`,
      'qual',
      scoreUtils.FindHighestScore(penaltyFreeHighScoreQual.filter((m) => m.event?.districtCode == district.code))
    );
    await StoreHighScores(
      frcCurrentSeason,
      `District${district.code}TBAPenaltyFree`,
      'playoff',
      scoreUtils.FindHighestScore(TBAPenaltyFreeHighScorePlayoff.filter((m) => m.event?.districtCode == district.code))
    );
    await StoreHighScores(
      frcCurrentSeason,
      `District${district.code}TBAPenaltyFree`,
      'qual',
      scoreUtils.FindHighestScore(TBAPenaltyFreeHighScoreQual.filter((m) => m.event?.districtCode == district.code))
    );
  }
};
