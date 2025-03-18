import * as requestUtils from './requestUtils';
import logger from '../logger';
import { StoreHighScores } from './storageUtils';
import * as scoreUtils from './scoreUtils';
import { ReadSecret } from './secretUtils';
import { BuildHybridSchedule } from './scheduleUtils';

const frcCurrentSeason = +(await ReadSecret('FRCCurrentSeason'));

export const UpdateHighScores = async () => {
  const eventList = await requestUtils.GetDataFromFIRST<EventListResponse>(`${frcCurrentSeason}/events`);
  const districts = await requestUtils.GetDataFromFIRST<DistrictListResponse>(`${frcCurrentSeason}/districts`);
  const promises = [];
  const order = [];
  const currentDate = new Date();
  currentDate.setDate(currentDate.getDate() + 1);
  logger.info(`Found ${eventList.body.Events.length} events for ${frcCurrentSeason}`);
  for (const _event of eventList.body.Events) {
    const eventDate = new Date(_event.dateStart);
    if (eventDate < currentDate) {
      promises.push(
        BuildHybridSchedule(frcCurrentSeason, _event.code, 'qual').catch((_) => {
          return null;
        })
      );
      promises.push(
        BuildHybridSchedule(frcCurrentSeason, _event.code, 'playoff').catch((_) => {
          return null;
        })
      );
      order.push({
        eventCode: _event.code,
        districtCode: _event.districtCode,
        type: 'qual'
      });
      order.push({
        eventCode: _event.code,
        districtCode: _event.districtCode,
        type: 'playoff'
      });
    }
  }
  const events = await Promise.all(promises);
  const matches = [];
  logger.info(`Retrieved data for ${events.length} events`);
  for (const _event of events) {
    const evt = order[events.indexOf(_event)];
    if (!!_event && _event.schedule.length > 0) {
      for (const match of _event.schedule) {
        // TODO: find a better way to filter these demo teams out, this way is not sustainable
        // FIRST says teams >=9970 and <=9999 are offseason demo teams
        if (
          match.postResultTime &&
          match.postResultTime !== '' &&
          match.teams.filter((t) => t.teamNumber >= 9986 && t.teamNumber <= 9999).length === 0
        ) {
          // Result was posted, and it's not all demo teams, so the match has occurred
          matches.push({
            event: evt,
            match
          });
        }
      }
    } else {
      logger.info(`Event ${evt.eventCode}, ${evt.type} has no schedule data, likely occurs in the future`);
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
  const districtOverallHighScorePlayoff: {
    event: { eventCode: string; districtCode: string; type: string };
    match: HybridMatch;
  }[] = [];
  const districtOverallHighScoreQual: {
    event: { eventCode: string; districtCode: string; type: string };
    match: HybridMatch;
  }[] = [];
  const districtPenaltyFreeHighScorePlayoff: EventMatch[] = [];
  const districtPenaltyFreeHighScoreQual: EventMatch[] = [];
  const TBADistrictPenaltyDreeHighScorePlayoff: EventMatch[] = [];
  const TBADistrictPenaltyFreeHighScoreQual: EventMatch[] = [];
  const districtOffsettingPenaltyHighScorePlayoff: EventMatch[] = [];
  const districtOffsettingPenaltyHighScoreQual: EventMatch[] = [];
  logger.info(`Found ${matches.length} total matches with data`);
  for (const match of matches) {
    if (match.event.type === 'playoff') {
      // Load match results into overall playoff bucket
      overallHighScorePlayoff.push(match);
      if (match.event.districtCode) districtOverallHighScorePlayoff.push(match);
    }
    if (match.event.type === 'qual') {
      // Load match results into overall quals bucket
      overallHighScoreQual.push(match);
      if (match.event.districtCode) districtOverallHighScoreQual.push(match);
    }
    if (
      match.event.type === 'playoff' &&
      ((match.match.scoreBlueFoul === 0 && match.match.scoreBlueFinal >= match.match.scoreRedFinal) ||
        (match.match.scoreRedFoul === 0 && match.match.scoreBlueFinal <= match.match.scoreRedFinal))
    ) {
      // Load match results into TBA playoff bucket because the winning Alliance had no fouls
      TBAPenaltyFreeHighScorePlayoff.push(match);
      if (match.event.districtCode) TBADistrictPenaltyDreeHighScorePlayoff.push(match);
    }
    if (
      match.event.type === 'qual' &&
      ((match.match.scoreBlueFoul === 0 && match.match.scoreBlueFinal >= match.match.scoreRedFinal) ||
        (match.match.scoreRedFoul === 0 && match.match.scoreBlueFinal <= match.match.scoreRedFinal))
    ) {
      // Load match results into TBA playoff bucket because the winning Alliance had no fouls
      TBAPenaltyFreeHighScoreQual.push(match);
      if (match.event.districtCode) TBADistrictPenaltyFreeHighScoreQual.push(match);
    }
    if (match.event.type === 'playoff' && match.match.scoreBlueFoul === 0 && match.match.scoreRedFoul === 0) {
      // Load match results into penalty free playoff bucket
      penaltyFreeHighScorePlayoff.push(match);
      if (match.event.districtCode) districtPenaltyFreeHighScorePlayoff.push(match);
    } else if (match.event.type === 'qual' && match.match.scoreBlueFoul === 0 && match.match.scoreRedFoul === 0) {
      // Load match results into penalty free playoff bucket
      penaltyFreeHighScoreQual.push(match);
      if (match.event.districtCode) districtPenaltyFreeHighScoreQual.push(match);
    } else if (
      match.event.type === 'playoff' &&
      // Load match results into offsetting fouls playoff bucket
      match.match.scoreBlueFoul === match.match.scoreRedFoul &&
      match.match.scoreBlueFoul > 0
    ) {
      offsettingPenaltyHighScorePlayoff.push(match);
      if (match.event.districtCode) districtOffsettingPenaltyHighScorePlayoff.push(match);
    } else if (
      match.event.type === 'qual' &&
      match.match.scoreBlueFoul === match.match.scoreRedFoul &&
      match.match.scoreBlueFoul > 0
    ) {
      // Load match results into offsetting fouls playoff bucket
      offsettingPenaltyHighScoreQual.push(match);
      if (match.event.districtCode) districtOffsettingPenaltyHighScoreQual.push(match);
    }
  }

  // Set up the promises to find and store the high scores
  const highScorePromises = [];
  highScorePromises.push(
    StoreHighScores(frcCurrentSeason, 'overall', 'playoff', scoreUtils.FindHighestScore(overallHighScorePlayoff))
  );
  highScorePromises.push(
    StoreHighScores(frcCurrentSeason, 'overall', 'qual', scoreUtils.FindHighestScore(overallHighScoreQual))
  );
  highScorePromises.push(
    StoreHighScores(
      frcCurrentSeason,
      'TBAPenaltyFree',
      'playoff',
      scoreUtils.FindHighestScore(TBAPenaltyFreeHighScorePlayoff)
    )
  );
  highScorePromises.push(
    StoreHighScores(
      frcCurrentSeason,
      'TBAPenaltyFree',
      'qual',
      scoreUtils.FindHighestScore(TBAPenaltyFreeHighScorePlayoff)
    )
  );
  highScorePromises.push(
    StoreHighScores(
      frcCurrentSeason,
      'penaltyFree',
      'playoff',
      scoreUtils.FindHighestScore(penaltyFreeHighScorePlayoff)
    )
  );
  highScorePromises.push(
    StoreHighScores(frcCurrentSeason, 'penaltyFree', 'qual', scoreUtils.FindHighestScore(penaltyFreeHighScoreQual))
  );
  highScorePromises.push(
    StoreHighScores(
      frcCurrentSeason,
      'offsetting',
      'playoff',
      scoreUtils.FindHighestScore(offsettingPenaltyHighScorePlayoff)
    )
  );
  highScorePromises.push(
    StoreHighScores(frcCurrentSeason, 'offsetting', 'qual', scoreUtils.FindHighestScore(offsettingPenaltyHighScoreQual))
  );
  districts.body.districts.forEach((district) => {
    highScorePromises.push(
      StoreHighScores(
        frcCurrentSeason,
        `${district.code}Overall`,
        'playoff',
        scoreUtils.FindHighestDistrictScore(districtOverallHighScorePlayoff, district.code)
      )
    );
    highScorePromises.push(
      StoreHighScores(
        frcCurrentSeason,
        `${district.code}Overall`,
        'qual',
        scoreUtils.FindHighestDistrictScore(districtOverallHighScoreQual, district.code)
      )
    );
    highScorePromises.push(
      StoreHighScores(
        frcCurrentSeason,
        `${district.code}offsetting`,
        'playoff',
        scoreUtils.FindHighestDistrictScore(districtOffsettingPenaltyHighScorePlayoff, district.code)
      )
    );
    highScorePromises.push(
      StoreHighScores(
        frcCurrentSeason,
        `${district.code}offsetting`,
        'qual',
        scoreUtils.FindHighestDistrictScore(districtOffsettingPenaltyHighScoreQual, district.code)
      )
    );
    highScorePromises.push(
      StoreHighScores(
        frcCurrentSeason,
        `${district.code}offsetting`,
        'playoff',
        scoreUtils.FindHighestDistrictScore(districtPenaltyFreeHighScorePlayoff, district.code)
      )
    );
    highScorePromises.push(
      StoreHighScores(
        frcCurrentSeason,
        `${district.code}offsetting`,
        'qual',
        scoreUtils.FindHighestDistrictScore(districtPenaltyFreeHighScoreQual, district.code)
      )
    );
    highScorePromises.push(
      StoreHighScores(
        frcCurrentSeason,
        `${district.code}TBAPenaltyFree`,
        'playoff',
        scoreUtils.FindHighestDistrictScore(TBADistrictPenaltyDreeHighScorePlayoff, district.code)
      )
    );
    highScorePromises.push(
      StoreHighScores(
        frcCurrentSeason,
        `${district.code}TBAPenaltyFree`,
        'qual',
        scoreUtils.FindHighestDistrictScore(TBADistrictPenaltyFreeHighScoreQual, district.code)
      )
    );
  });

  await Promise.all(highScorePromises);
};
