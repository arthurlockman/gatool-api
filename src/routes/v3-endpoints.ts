import express from 'express';
import * as requestUtils from '../utils/requestUtils';
import * as scoreUtils from '../utils/scoreUtils';
import {
  GetHighScores,
  GetTeamUpdateHistory,
  GetTeamUpdates,
  GetUserPreferences,
  StoreTeamUpdates,
  StoreUserPreferences
} from '../utils/storageUtils';
import { ReadSecret } from '../utils/secretUtils';
import * as redis from 'redis';
import { BuildHybridSchedule } from '../utils/scheduleUtils';
import logger from '../logger';

export const router = express.Router();

const frcCurrentSeason = await ReadSecret('FRCCurrentSeason');

const redisDisabled = process.env.DISABLE_REDIS === 'true';

let redisClient = null;
if (!redisDisabled) {
  redisClient = redis.createClient({
    url: 'redis://gatool-redis-01:6379'
  });
  redisClient.on('error', (error) => logger.error(`Error : ${error}`));
  await redisClient.connect();
} else {
  logger.warn('Redis disabled by CLI argument.');
}

const getRedisItem = async (key: string) => {
  return redisDisabled ? null : await redisClient?.get(key);
}

const setRedisItem = async (key: string, value: string, expiration: number) => {
  if (!redisDisabled) {
    await redisClient?.set(key, value, {
      EX: expiration
    });
  }
}


// Common data getters

const GetTeams = async (
  year: string,
  eventCode: string | null = null,
  districtCode: string | null = null,
  teamNumber: string | null = null
) => {
  const query = [];
  if (eventCode) {
    query.push(`eventCode=${eventCode}`);
  }
  if (districtCode) {
    query.push(`districtCode=${districtCode}`);
  }
  if (teamNumber) {
    query.push(`teamNumber=${teamNumber}`);
  }
  const teamData = await requestUtils.GetDataFromFIRST<TeamResponse>(`${year}/teams?${query.join('&')}&page=1`);
  if (teamData.body.pageTotal === 1) {
    return teamData.body;
  } else {
    const promises = [];
    for (let i = 2; i <= teamData.body.pageTotal; i++) {
      promises.push(requestUtils.GetDataFromFIRST<TeamResponse>(`${year}/teams?${query.join('&')}&page=${i}`));
    }
    const allTeamData = await Promise.all(promises);
    allTeamData.map((team) => {
      teamData.body.teamCountPage += team.body.teamCountPage;
      teamData.body.teams = teamData.body.teams.concat(team.body.teams);
    });
    teamData.body.pageTotal = 1;
    return teamData.body;
  }
};

// Routes

router.get('/:year/teams', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=600');
  if (req.query === null) {
    res.statusCode = 400;
    res.send({ message: 'You must supply query parameters.' });
  }
  const eventCode = req.query.eventCode as string;
  const districtCode = req.query.districtCode as string;
  const teamNumber = req.query.teamNumber as string;
  res.json(await GetTeams(req.params.year, eventCode, districtCode, teamNumber));
});

router.get('/:year/schedule/:eventCode/:tournamentLevel', async (req, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  const response = await requestUtils.GetDataFromFIRST(
    `${req.params.year}/schedule/${req.params.eventCode}/${req.params.tournamentLevel}`
  );
  res.json(response.body);
});

router.get('/:year/districts/', async (req, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  const response = await requestUtils.GetDataFromFIRST(`${req.params.year}/districts/`);
  res.json(response.body);
});

router.get('/:year/matches/:eventCode/:tournamentLevel', async (req, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  const response = await requestUtils.GetDataFromFIRST(
    `${req.params.year}/matches/${req.params.eventCode}/${req.params.tournamentLevel}`
  );
  res.json(response.body);
});

router.get('/:year/schedule/hybrid/:eventCode/:tournamentLevel', async (req, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  const schedule = await BuildHybridSchedule(+req.params.year, req.params.eventCode, req.params.tournamentLevel);
  res.json({
    Schedule: schedule
  });
});

router.get('/:year/awards/event/:eventCode', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=300');
  const response = await requestUtils.GetDataFromFIRST(`${req.params.year}/awards/event/${req.params.eventCode}`);
  res.json(response.body);
});

router.get('/:year/events', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=86400');
  const response = await requestUtils.GetDataFromFIRST(`${req.params.year}/events`);
  res.json(response.body);
});

router.get('/:year/scores/:eventCode/:tournamentLevel/:start/:end', async (req, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  let response;
  if (req.params.start === req.params.end) {
    response = await requestUtils.GetDataFromFIRST(
      `${req.params.year}/scores/${req.params.eventCode}/${req.params.tournamentLevel}?matchNumber=${req.params.start}`
    );
  } else {
    response = await requestUtils.GetDataFromFIRST(
      `${req.params.year}/scores/${req.params.eventCode}/${req.params.tournamentLevel}?start=${req.params.start}&end=${req.params.end}`
    );
  }
  res.json(response.body);
});

router.get('/:year/scores/:eventCode/playoff', async (req, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  const response = await requestUtils.GetDataFromFIRST(`${req.params.year}/scores/${req.params.eventCode}/Playoff`);
  res.json(response.body);
});

router.get('/:year/scores/:eventCode/qual', async (req, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  const response = await requestUtils.GetDataFromFIRST(`${req.params.year}/scores/${req.params.eventCode}/Qual`);
  res.json(response.body);
});

router.get('/:year/communityUpdates/:eventCode', async (req, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  const teamList = await GetTeams(req.params.year, req.params.eventCode);
  const teamData = await Promise.all(
    teamList.teams.map(async (t) => {
      try {
        return { teamNumber: t.teamNumber, updates: JSON.parse(await GetTeamUpdates(t.teamNumber)) };
      } catch (_e) {
        return { teamNumber: t.teamNumber, updates: null };
      }
    })
  );
  res.json(teamData);
});

router.get('/team/:teamNumber/updates', async (req, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  try {
    const updates = await GetTeamUpdates(+req.params.teamNumber);
    res.json(JSON.parse(updates));
  } catch (e) {
    logger.error(e);
    res.status(404).send(`No updates found for team ${req.params.teamNumber}`);
  }
});

router.put('/team/:teamNumber/updates', async (req, res) => {
  await StoreTeamUpdates(+req.params.teamNumber, req.body, req.auth?.payload.email as string);
  res.status(204).send();
});

router.get('/team/:teamNumber/updates/history', async (req, res) => {
  const r = await GetTeamUpdateHistory(+req.params.teamNumber);
  res.json(r);
});

const getAwards = async (season: number, team: number) => {
  let currentYearAwards, pastYearAwards, secondYearAwards;
  try {
    currentYearAwards = await requestUtils.GetDataFromFIRST(`${season}/awards/team/${team}`);
  } catch (_) {
    currentYearAwards = null;
  }
  try {
    pastYearAwards = await requestUtils.GetDataFromFIRST(`${season - 1}/awards/team/${team}`);
  } catch (_) {
    pastYearAwards = null;
  }
  try {
    secondYearAwards = await requestUtils.GetDataFromFIRST(`${season - 2}/awards/team/${team}`);
  } catch (_) {
    secondYearAwards = null;
  }
  const awardList: any = {};
  awardList[`${season}`] = currentYearAwards ? currentYearAwards.body : null;
  awardList[`${season - 1}`] = pastYearAwards ? pastYearAwards.body : null;
  awardList[`${season - 2}`] = secondYearAwards ? secondYearAwards.body : null;
  return awardList;
};

router.get('/team/:teamNumber/awards', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=300');
  const currentSeason = parseInt(frcCurrentSeason, 10);
  res.json(await getAwards(currentSeason, +req.params.teamNumber));
});

router.get('/:currentSeason/team/:teamNumber/awards', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=300');
  const currentSeason = parseInt(req.params.currentSeason, 10);
  res.json(await getAwards(currentSeason, +req.params.teamNumber));
});

router.get('/team/:teamNumber/appearances', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=3600');
  const key = `team/frc${req.params.teamNumber}/events`;
  const cacheResults = await getRedisItem(`tbaapi:${key}`);
  if (cacheResults) {
    res.json(JSON.parse(cacheResults));
  } else {
    const response = await requestUtils.GetDataFromTBA(key);
    await setRedisItem(`tbaapi:${key}`, JSON.stringify(response.body), 259200);
    res.json(response.body);
  }
});

router.get('/:year/team/:teamNumber/media', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=3600');
  const currentSeason = parseInt(req.params.year, 10);
  const key = `team/frc${req.params.teamNumber}/media/${currentSeason}`;
  const cacheResults = await getRedisItem(`tbaapi:${key}`);
  if (cacheResults) {
    res.json(JSON.parse(cacheResults));
  } else {
    const response = await requestUtils.GetDataFromTBA(key);
    res.json(response.body);
    await setRedisItem(`tbaapi:${key}`, JSON.stringify(response.body), 259200);
  }
});

router.get('/:year/awards/team/:teamNumber', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=300');
  const response = await requestUtils.GetDataFromFIRST(`${req.params.year}/awards/team/${req.params.teamNumber}`);
  res.json(response.body);
});

router.get('/:year/avatars/team/:teamNumber/avatar.png', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=2629800');
  try {
    const key = `${req.params.year}/avatars?teamNumber=${req.params.teamNumber}`;
    const cacheResults = await getRedisItem(`frcapi:${key}`);
    let encodedAvatar;
    if (cacheResults) {
      encodedAvatar = cacheResults;
    } else {
      const avatar = await requestUtils.GetDataFromFIRST<TeamAvatarResponse>(key);
      const teamAvatar = avatar.body.teams[0];
      if (teamAvatar.encodedAvatar == null) {
        res.status(404);
        res.json({ message: 'Avatar not found' });
      }
      encodedAvatar = teamAvatar.encodedAvatar;
      await setRedisItem(`frcapi:${key}`, encodedAvatar, 604800);
    }
    res.setHeader('Content-Type', 'image/png');
    res.setHeader('Charset', 'utf-8');
    res.send(Buffer.from(encodedAvatar, 'base64'));
  } catch (e: any) {
    const statusCode = e?.response?.statusCode ? parseInt(e.response.statusCode, 10) : 404;
    const message = e?.response?.body ? e.response.body : 'Avatar not found.';
    res.status(statusCode);
    res.json({ message });
  }
});

router.get('/:year/rankings/:eventCode', async (req, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  const response = await requestUtils.GetDataFromFIRST(`${req.params.year}/rankings/${req.params.eventCode}`);
  res.json({ rankings: response.body, headers: response.headers });
});

router.get('/:year/alliances/:eventCode', async (req, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  const response = await requestUtils.GetDataFromFIRST(`${req.params.year}/alliances/${req.params.eventCode}`);
  res.json(response.body);
});

router.get('/:year/offseason/teams/:eventCode/:page', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=600');
  const response = await requestUtils.GetDataFromTBA<TBATeamResponse>(`event/${req.params.eventCode}/teams`);
  const teams = response.body;
  teams.sort(function (a, b) {
    return a.team_number - b.team_number;
  });
  const result = [];
  for (let i = 1; i < teams.length; i++) {
    try {
      const tmp = {
        teamNumber: teams[i].team_number,
        nameFull: teams[i].name,
        nameShort: teams[i].nickname,
        schoolName: null,
        city: teams[i].city,
        stateProv: teams[i].state_prov,
        country: teams[i].country,
        website: teams[i].website,
        rookieYear: teams[i].rookie_year,
        robotName: null,
        districtCode: null,
        homeCMP: null
      };
      result.push(tmp);
    } catch (ex) {
      logger.error(ex, `Error parsing event data: ${JSON.stringify(teams[i])}`);
    }
  }
  res.json({
    teams: result,
    teamCountTotal: result.length,
    teamCountPage: result.length,
    pageCurrent: 1,
    pageTotal: 1
  });
});

router.get('/:year/offseason/events', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=86400');
  const response = await requestUtils.GetDataFromTBA<TBAEventResponse>(`events/${req.params.year}`);
  const events = response.body;
  const result = [];
  for (let i = 1; i < events.length; i++) {
    try {
      if (events[i].event_type_string === 'Offseason') {
        let address = 'no address, no city, no state, no country';
        if (events[i].address) {
          address = events[i].address;
        }
        const tmp = {
          code: events[i].key,
          divisionCode: events[i].event_code,
          name: events[i].short_name,
          type: events[i].event_type_string,
          districtCode: events[i].district?.abbreviation,
          venue: events[i].location_name,
          address: address.split(', ')[0],
          city: address.split(', ')[1],
          stateprov: address.split(', ')[2],
          country: address.split(', ')[3],
          website: events[i].website,
          timezone: events[i].timezone,
          dateStart: events[i].start_date,
          dateEnd: events[i].end_date
        };
        result.push(tmp);
      }
    } catch (ex) {
      logger.error(ex, `Error parsing event data: ${JSON.stringify(events[i])}`);
    }
  }
  res.json({
    Events: result,
    eventCount: result.length
  });
});

router.get('/:year/district/rankings/:districtCode', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=86400');
  const query = [];
  query.push(`districtCode=${req.params.districtCode}`);
  if (req.query) {
    const top = req.query.top;
    if (top) {
      query.push(`top=${top}`);
    }
  }
  const rankingData = await requestUtils.GetDataFromFIRST<DistrictRankingsResponse>(
    `${req.params.year}/rankings/district?${query.join('&')}&page=1`
  );
  if (rankingData.body.pageTotal === 1) {
    res.json(rankingData.body);
  } else {
    const promises = [];
    for (let i = 2; i <= rankingData.body.pageTotal; i++) {
      promises.push(
        requestUtils.GetDataFromFIRST<DistrictRankingsResponse>(
          `${req.params.year}/rankings/district?${query.join('&')}&page=${i}`
        )
      );
    }
    const allRankData = await Promise.all(promises);
    allRankData.map((districtRank) => {
      rankingData.body.rankingCountPage += districtRank.body.rankingCountPage;
      rankingData.body.districtRanks = rankingData.body.districtRanks.concat(districtRank.body.districtRanks);
    });
    rankingData.body.pageTotal = 1;
    res.json(rankingData.body);
  }
});

router.get('/:year/highscores/:eventCode', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=600');
  const eventList = await requestUtils.GetDataFromFIRST<EventListResponse>(`${req.params.year}/events/`);
  const evtList = eventList.body.Events.filter((evt) => evt.code === req.params.eventCode);
  if (evtList.length !== 1) {
    res.status(404).send('Event not found');
  }
  const eventDetails = evtList[0];
  const qualMatchList = await BuildHybridSchedule(+req.params.year, req.params.eventCode, 'qual');
  const playoffMatchList = await BuildHybridSchedule(+req.params.year, req.params.eventCode, 'playoff');

  let matches = qualMatchList.schedule
    .map((x) => {
      return {
        event: { eventCode: eventDetails.code, districtCode: eventDetails.districtCode, type: 'qual' },
        match: x
      };
    })
    .concat(
      playoffMatchList.schedule.map((x) => {
        return {
          event: { eventCode: eventDetails.code, districtCode: eventDetails.districtCode, type: 'playoff' },
          match: x
        };
      })
    );
  matches = matches.filter(
    (match) =>
      match.match.postResultTime &&
      match.match.postResultTime !== '' &&
      // TODO: find a better way to filter these demo teams out, this way is not sustainable
      match.match.teams.filter((t) => t.teamNumber >= 9986 && t.teamNumber <= 9999).length === 0
  );

  const overallHighScorePlayoff = [];
  const overallHighScoreQual = [];
  const penaltyFreeHighScorePlayoff = [];
  const penaltyFreeHighScoreQual = [];
  const TBAPenaltyFreeHighScorePlayoff = [];
  const TBAPenaltyFreeHighScoreQual = [];
  const offsettingPenaltyHighScorePlayoff = [];
  const offsettingPenaltyHighScoreQual = [];
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
  const highScoresData = [];
  if (overallHighScorePlayoff.length > 0) {
    highScoresData.push(
      scoreUtils.BuildHighScoreJson(
        +req.params.year,
        'overall',
        'playoff',
        scoreUtils.FindHighestScore(overallHighScorePlayoff)
      )
    );
  }
  if (overallHighScoreQual.length > 0) {
    highScoresData.push(
      scoreUtils.BuildHighScoreJson(
        +req.params.year,
        'overall',
        'qual',
        scoreUtils.FindHighestScore(overallHighScoreQual)
      )
    );
  }
  if (penaltyFreeHighScorePlayoff.length > 0) {
    highScoresData.push(
      scoreUtils.BuildHighScoreJson(
        +req.params.year,
        'penaltyFree',
        'playoff',
        scoreUtils.FindHighestScore(penaltyFreeHighScorePlayoff)
      )
    );
  }
  if (penaltyFreeHighScoreQual.length > 0) {
    highScoresData.push(
      scoreUtils.BuildHighScoreJson(
        +req.params.year,
        'penaltyFree',
        'qual',
        scoreUtils.FindHighestScore(penaltyFreeHighScoreQual)
      )
    );
  }
  if (offsettingPenaltyHighScorePlayoff.length > 0) {
    highScoresData.push(
      scoreUtils.BuildHighScoreJson(
        +req.params.year,
        'offsetting',
        'playoff',
        scoreUtils.FindHighestScore(offsettingPenaltyHighScorePlayoff)
      )
    );
  }
  if (offsettingPenaltyHighScoreQual.length > 0) {
    highScoresData.push(
      scoreUtils.BuildHighScoreJson(
        +req.params.year,
        'offsetting',
        'qual',
        scoreUtils.FindHighestScore(offsettingPenaltyHighScoreQual)
      )
    );
  }
  if (TBAPenaltyFreeHighScoreQual.length > 0) {
    highScoresData.push(
      scoreUtils.BuildHighScoreJson(
        +req.params.year,
        'TBAPenaltyFree',
        'qual',
        scoreUtils.FindHighestScore(TBAPenaltyFreeHighScoreQual)
      )
    );
  }
  if (TBAPenaltyFreeHighScorePlayoff.length > 0) {
    highScoresData.push(
      scoreUtils.BuildHighScoreJson(
        +req.params.year,
        'TBAPenaltyFree',
        'playoff',
        scoreUtils.FindHighestScore(TBAPenaltyFreeHighScorePlayoff)
      )
    );
  }
  res.json(highScoresData);
});

router.get('/:year/highscores', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=600');
  const scores = await GetHighScores(req.params.year);
  res.json(scores);
});

// User Data Storage

router.get('/user/preferences', async (req, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  try {
    const email = req.auth?.payload.email as string;
    const prefs = await GetUserPreferences(email);
    res.json(JSON.parse(prefs));
  } catch (e) {
    logger.error(e);
    res.status(404).send();
  }
});

router.put('/user/preferences', async (req, res) => {
  const email = req.auth?.payload.email as string;
  await StoreUserPreferences(email, req.body);
  res.status(204).send();
});
