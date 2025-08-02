import express from 'express';
import * as requestUtils from '../utils/requestUtils';
import * as scoreUtils from '../utils/scoreUtils';
import {
  GetFTCHighScores,
  GetFTCTeamUpdateHistory,
  GetFTCTeamUpdates,
  StoreFTCTeamUpdates
} from '../utils/storageUtils';
import { ReadSecret } from '../utils/secretUtils';
import logger from '../logger';
import { getRedisItem, REDIS_RETENTION_12_HOUR, REDIS_RETENTION_14_DAY, REDIS_RETENTION_3_DAY, REDIS_RETENTION_7_DAY, setRedisItem  } from '../clients/redisClient';
import PQueue from 'p-queue';

export const router = express.Router();

const frcCurrentSeason = await ReadSecret('FRCCurrentSeason');

const TOAEventTypes = <any>{
  LGMEET: 'League/Meet',
  OTHER: 'Other',
  QUAL: 'Qualifier',
  RCMP: 'Region Championship',
  SCRIMMAGE: 'Scrimmage',
  SPRING: 'Spring Event',
  LGCMP: 'League Championship',
  OFFSSN: 'Off Season',
  SPRQUAL: 'Super Qualifier',
  SPRRGNL: 'Super Regional',
  WRLDCMP: 'World Championship'
};

// Common data getters

const GetFTCTeams = async (
  year: string,
  eventCode: string | null = null,
  state: string | null = null,
  teamNumber: string | null = null
) => {
  const query = [];
  if (eventCode) {
    query.push(`eventCode=${eventCode}`);
  }
  if (state) {
    query.push(`state=${state}`);
  }
  if (teamNumber) {
    query.push(`teamNumber=${teamNumber}`);
  }
  const teamData = await requestUtils.GetDataFromFTC<FTCTeamResponse>(`${year}/teams?${query.join('&')}&page=1`);
  if (teamData.body.pageTotal === 1) {
    return teamData.body;
  } else {
    const promises = [];
    for (let i = 2; i <= teamData.body.pageTotal; i++) {
      promises.push(requestUtils.GetDataFromFTC<FTCTeamResponse>(`${year}/teams?${query.join('&')}&page=${i}`));
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
  const teamNumber = req.query.teamNumber as string;
  const state = req.query.state as string;
  res.json(await GetFTCTeams(req.params.year, eventCode, state, teamNumber));
});

router.get('/:year/schedule/:eventCode/:tournamentLevel', async (req, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  const response = await requestUtils.GetDataFromFTC(
    `${req.params.year}/schedule/${req.params.eventCode}/?tournamentLevel=${req.params.tournamentLevel}`
  );
  res.json(response.body);
});

router.get('/:year/leagues/', async (req, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  const response = await requestUtils.GetDataFromFTC(`${req.params.year}/leagues`);
  res.json(response.body);
});

router.get('/:year/matches/:eventCode/:tournamentLevel', async (req, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  const response = await requestUtils.GetDataFromFTC(
    `${req.params.year}/matches/${req.params.eventCode}?tournamentLevel=${req.params.tournamentLevel}`
  );
  res.json(response.body);
});

router.get('/:year/schedule/hybrid/:eventCode/:tournamentLevel', async (req, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  const schedule = await requestUtils.GetDataFromFTC(
    `${req.params.year}/schedule/${req.params.eventCode}/${req.params.tournamentLevel}/hybrid`
  );
  res.json(schedule.body);
});

router.get('/:year/awards/event/:eventCode', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=300');
  const response = await requestUtils.GetDataFromFTC(`${req.params.year}/awards/${req.params.eventCode}`);
  res.json(response.body);
});

router.get('/:year/events', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=86400');
  const response = await requestUtils.GetDataFromFTC(`${req.params.year}/events`);
  res.json(response.body);
});

router.get('/:year/events/:eventcode', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=86400');
  const response = await requestUtils.GetDataFromFTC(`${req.params.year}/events?eventCode=${req.params.eventcode}`);
  res.json(response.body);
});

router.get('/:year/scores/:eventCode/:tournamentLevel/:start/:end', async (req, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  let response;
  if (req.params.start === req.params.end) {
    response = await requestUtils.GetDataFromFTC(
      `${req.params.year}/scores/${req.params.eventCode}/${req.params.tournamentLevel}?matchNumber=${req.params.start}`
    );
  } else {
    response = await requestUtils.GetDataFromFTC(
      `${req.params.year}/scores/${req.params.eventCode}/${req.params.tournamentLevel}?start=${req.params.start}&end=${req.params.end}`
    );
  }
  res.json(response.body);
});

router.get('/:year/scores/:eventCode/playoff', async (req, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  const response = await requestUtils.GetDataFromFTC(`${req.params.year}/scores/${req.params.eventCode}/Playoff`);
  res.json(response.body);
});

router.get('/:year/scores/:eventCode/qual', async (req, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  const response = await requestUtils.GetDataFromFTC(`${req.params.year}/scores/${req.params.eventCode}/Qual`);
  res.json(response.body);
});

router.get('/:year/communityUpdates/:eventCode', async (req, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  const teamList = await GetFTCTeams(req.params.year, req.params.eventCode);
  const teamData = await Promise.all(
    teamList.teams.map(async (t) => {
      try {
        return { teamNumber: t.teamNumber, updates: JSON.parse(await GetFTCTeamUpdates(t.teamNumber)) };
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
    const updates = await GetFTCTeamUpdates(+req.params.teamNumber);
    res.json(JSON.parse(updates));
  } catch (e) {
    logger.error(e);
    res.status(404).send(`No updates found for team ${req.params.teamNumber}`);
  }
});

router.put('/team/:teamNumber/updates', async (req, res) => {
  ensureUser(req, res);
  await StoreFTCTeamUpdates(+req.params.teamNumber, req.body, req.auth?.payload.email as string);
  res.status(204).send();
});

router.get('/team/:teamNumber/updates/history', async (req, res) => {
  const r = await GetFTCTeamUpdateHistory(+req.params.teamNumber);
  res.json(r);
});

const getFTCAwards = async (season: number, team: number) => {
  let currentYearAwards, pastYearAwards, secondYearAwards;
  try {
    currentYearAwards = await requestUtils.GetDataFromFTC(`${season}/awards/${team}`);
  } catch (_) {
    currentYearAwards = null;
  }
  try {
    pastYearAwards = await requestUtils.GetDataFromFTC(`${season - 1}/awards/${team}`);
  } catch (_) {
    pastYearAwards = null;
  }
  try {
    secondYearAwards = await requestUtils.GetDataFromFTC(`${season - 2}/awards/${team}`);
  } catch (_) {
    secondYearAwards = null;
  }
  const awardList: any = {};
  awardList[`${season}`] = currentYearAwards ? currentYearAwards.body : null;
  awardList[`${season - 1}`] = pastYearAwards ? pastYearAwards.body : null;
  awardList[`${season - 2}`] = secondYearAwards ? secondYearAwards.body : null;
  return awardList;
};

const getTeamAwards = async (season: number, team: number, cachePeriod: number = REDIS_RETENTION_14_DAY) => {
  let awards = null;
  const cacheKey = `frc:team:${team}:season:${season}:awards`;
  const cached = await getRedisItem(cacheKey);
  if (cached) {
    awards = JSON.parse(cached);
  } else {
    try {
      awards = await requestUtils.GetDataFromFTC(`${season - 1}/awards/${team}`);
      setRedisItem(cacheKey, JSON.stringify(awards), cachePeriod);
    } catch (_) {
      awards = null;
    }
  }
  return awards;
}

const getLast3YearAwards = async (season: number, team: number) => {
  const currentYearAwards = await getTeamAwards(season, team, REDIS_RETENTION_12_HOUR);
  const pastYearAwards = await getTeamAwards(season - 1, team);
  const secondYearAwards = await getTeamAwards(season - 2, team);
  const awardList: any = {};
  awardList[`${season}`] = currentYearAwards ? currentYearAwards.body : null;
  awardList[`${season - 1}`] = pastYearAwards ? pastYearAwards.body : null;
  awardList[`${season - 2}`] = secondYearAwards ? secondYearAwards.body : null;
  return awardList;
};

router.get('/team/:teamNumber/awards', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=300');
  const currentSeason = parseInt(frcCurrentSeason, 10);
  res.json(await getFTCAwards(currentSeason, +req.params.teamNumber));
});

router.get('/:currentSeason/team/:teamNumber/awards', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=300');
  const currentSeason = parseInt(req.params.currentSeason, 10);
  res.json(await getFTCAwards(currentSeason, +req.params.teamNumber));
});

router.post('/:currentSeason/queryAwards', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=300');
  const awards = new Map();
  const currentSeason = parseInt(req.params.currentSeason, 10);
  const awardDataQueue = new PQueue({
    concurrency: 10
  });
  awardDataQueue.on('completed', (result: any) => {
    awards.set(result[0], result[1]);
  });
  req.body.teams.forEach((team: number) => {
    awardDataQueue.add(async () => {
      return [team, await getLast3YearAwards(currentSeason, team)];
    })
  });
  await awardDataQueue.onIdle();
  res.json(Object.fromEntries(awards));
});

// Rework this for https://theorangealliance.org/apidocs
// TOA history endpoint requires a specific season, unlike TBA,
// so this might be one of those things we need to build on our side to cache.
router.get('/team/:teamNumber/appearances', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=3600');
  const key = `team/${req.params.teamNumber}/events`;
  const cacheResults = await getRedisItem(`toaapi:${key}`);
  if (cacheResults) {
    res.json(JSON.parse(cacheResults));
  } else {
    const response = await requestUtils.GetDataFromTOA(key);
    await setRedisItem(`toaapi:${key}`, JSON.stringify(response.body), REDIS_RETENTION_3_DAY);
    res.json(response.body);
  }
});

// TOA tracks regions with names and codes
router.get('/regions', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=3600');
  const key = `regions`;
  const cacheResults = await getRedisItem(`toaapi:${key}`);
  if (cacheResults) {
    res.json(JSON.parse(cacheResults));
  } else {
    const response = await requestUtils.GetDataFromTOA(key);
    await setRedisItem(`toaapi:${key}`, JSON.stringify(response.body), REDIS_RETENTION_14_DAY);
    res.json(response.body);
  }
});

// TOA tracks event types with names and codes
router.get('/event-types', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=3600');
  const key = `event-types`;
  const cacheResults = await getRedisItem(`toaapi:${key}`);
  if (cacheResults) {
    res.json(JSON.parse(cacheResults));
  } else {
    const response = await requestUtils.GetDataFromTOA(key);
    await setRedisItem(`toaapi:${key}`, JSON.stringify(response.body), REDIS_RETENTION_14_DAY);
    res.json(response.body);
  }
});

// rework this for https://theorangealliance.org/apidocs
// No media endpoint at TOA. No images for anything, in fact.
router.get('/:year/team/:teamNumber/media', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=3600');
  const currentSeason = parseInt(req.params.year, 10);
  const key = `team/${req.params.teamNumber}/media/${currentSeason}`;
  const cacheResults = await getRedisItem(`toaapi:${key}`);
  if (cacheResults) {
    res.json(JSON.parse(cacheResults));
  } else {
    const response = await requestUtils.GetDataFromTOA(key);
    res.json(response.body);
    await setRedisItem(`toaapi:${key}`, JSON.stringify(response.body), REDIS_RETENTION_3_DAY);
  }
});

router.get('/:year/awards/team/:teamNumber', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=300');
  const response = await requestUtils.GetDataFromFTC(`${req.params.year}/awards/${req.params.teamNumber}`);
  res.json(response.body);
});

// This is not available, but we're keeping it here in case it ever becomes available.
router.get('/:year/avatars/team/:teamNumber/avatar.png', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=2629800');
  try {
    const key = `${req.params.year}/avatars?teamNumber=${req.params.teamNumber}`;
    const cacheResults = await getRedisItem(`frcapi:${key}`);
    let encodedAvatar;
    if (cacheResults) {
      encodedAvatar = cacheResults;
    } else {
      const avatar = await requestUtils.GetDataFromFTC<TeamAvatarResponse>(key);
      const teamAvatar = avatar.body.teams[0];
      if (teamAvatar.encodedAvatar == null) {
        res.status(404);
        res.json({ message: 'Avatar not found' });
      }
      encodedAvatar = teamAvatar.encodedAvatar;
      await setRedisItem(`frcapi:${key}`, encodedAvatar, REDIS_RETENTION_7_DAY);
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
  const response = await requestUtils.GetDataFromFTC(`${req.params.year}/rankings/${req.params.eventCode}`);
  res.json({ rankings: response.body, headers: response.headers });
});

router.get('/:year/alliances/:eventCode', async (req, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  const response = await requestUtils.GetDataFromFTC(`${req.params.year}/alliances/${req.params.eventCode}`);
  res.json(response.body);
});

// Rework for https://theorangealliance.org/apidocs
router.get('/:year/offseason/teams/:eventCode', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=600');
  const response = await requestUtils.GetDataFromTOA<TOATeamResponse>(`event/${req.params.eventCode}/teams`);
  const teams = response.body;
  teams.sort(function (a, b) {
    return a.team_number - b.team_number;
  });
  const result = [];
  for (let i = 1; i < teams.length; i++) {
    try {
      const tmp = {
        teamNumber: teams[i].team_number,
        region: teams[i].region_key,
        league: teams[i].league_key,
        nameFull: teams[i].team_name_long,
        nameShort: teams[i].team_name_short,
        schoolName: null,
        city: teams[i].city,
        stateProv: teams[i].state_prov,
        country: teams[i].country,
        website: teams[i].website,
        rookieYear: teams[i].rookie_year,
        robotName: teams[i].robot_name,
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

// rework for https://theorangealliance.org/apidocs
router.get('/:year/offseason/events', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=86400');
  const response = await requestUtils.GetDataFromTOA<TOAEventResponse>(`events/${req.params.year}`);
  const events = response.body;
  const result = [];
  for (let i = 1; i < events.length; i++) {
    try {
      if (events[i].event_type_key === 'OFFSSN') {
        const tmp = {
          code: events[i].event_code,
          divisionCode: events[i].division_key,
          name: events[i].event_name,
          type: TOAEventTypes[events[i].event_type_key] || null,
          leagueCode: events[i].league_key,
          venue: events[i].venue,
          address: null,
          city: events[i].city,
          stateprov: events[i].state_prov,
          country: events[i].country,
          website: events[i].website,
          timezone: events[i].time_zone,
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
    events: result,
    eventCount: result.length
  });
});

router.get('/:year/leagues/rankings/:regionCode/:leagueCode', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=86400');
  const rankingData = await requestUtils.GetDataFromFTC<LeagueRankingsResponse>(
    `${req.params.year}/leagues/rankings/${req.params.regionCode}/${req.params.leagueCode}`
  );
  res.json(rankingData.body);
});

// make this work for FTC
router.get('/:year/highscores/:eventCode', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=600');
  const eventList = await requestUtils.GetDataFromFTC<FTCEventListResponse>(
    `${req.params.year}/events?eventCode=${req.params.eventCode}`
  );
  const evtList = eventList.body.events.filter((evt) => evt.code === req.params.eventCode);
  if (evtList.length !== 1) {
    res.status(404).send('Event not found');
  }
  const eventDetails = evtList[0];
  const response = await requestUtils.GetDataFromFTC<FTCHybridScheduleResponse>(
    `${req.params.year}/schedule/${req.params.eventCode}/qual/hybrid`
  );
  const qualMatchList = response.body;
  const response2 = await requestUtils.GetDataFromFTC<FTCHybridScheduleResponse>(
    `${req.params.year}/schedule/${req.params.eventCode}/playoff/hybrid`
  );
  const playoffMatchList = response2.body;

  let matches = qualMatchList.schedule
    .map((x) => {
      return {
        event: {
          eventCode: eventDetails.code,
          regionCode: eventDetails.regionCode,
          leagueCode: eventDetails.leagueCode,
          type: 'qual'
        },
        match: x
      };
    })
    .concat(
      playoffMatchList.schedule.map((x) => {
        return {
          event: {
            eventCode: eventDetails.code,
            regionCode: eventDetails.regionCode,
            leagueCode: eventDetails.leagueCode,
            type: 'playoff'
          },
          match: x
        };
      })
    );
  matches = matches.filter((match) => match.match.postResultTime && match.match.postResultTime !== '');

  const overallHighScorePlayoff = [];
  const overallHighScoreQual = [];
  const penaltyFreeHighScorePlayoff = [];
  const penaltyFreeHighScoreQual = [];
  const TOAPenaltyFreeHighScorePlayoff = [];
  const TOAPenaltyFreeHighScoreQual = [];
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
      // Load match results into TOA playoff bucket because the winning Alliance had no fouls
      TOAPenaltyFreeHighScorePlayoff.push(match);
    }
    if (
      match.event.type === 'qual' &&
      ((match.match.scoreBlueFoul === 0 && match.match.scoreBlueFinal >= match.match.scoreRedFinal) ||
        (match.match.scoreRedFoul === 0 && match.match.scoreBlueFinal <= match.match.scoreRedFinal))
    ) {
      // Load match results into TOA playoff bucket because the winning Alliance had no fouls
      TOAPenaltyFreeHighScoreQual.push(match);
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
        scoreUtils.FindFTCHighestScore(overallHighScorePlayoff)
      )
    );
  }
  if (overallHighScoreQual.length > 0) {
    highScoresData.push(
      scoreUtils.BuildHighScoreJson(
        +req.params.year,
        'overall',
        'qual',
        scoreUtils.FindFTCHighestScore(overallHighScoreQual)
      )
    );
  }
  if (penaltyFreeHighScorePlayoff.length > 0) {
    highScoresData.push(
      scoreUtils.BuildHighScoreJson(
        +req.params.year,
        'penaltyFree',
        'playoff',
        scoreUtils.FindFTCHighestScore(penaltyFreeHighScorePlayoff)
      )
    );
  }
  if (penaltyFreeHighScoreQual.length > 0) {
    highScoresData.push(
      scoreUtils.BuildHighScoreJson(
        +req.params.year,
        'penaltyFree',
        'qual',
        scoreUtils.FindFTCHighestScore(penaltyFreeHighScoreQual)
      )
    );
  }
  if (offsettingPenaltyHighScorePlayoff.length > 0) {
    highScoresData.push(
      scoreUtils.BuildHighScoreJson(
        +req.params.year,
        'offsetting',
        'playoff',
        scoreUtils.FindFTCHighestScore(offsettingPenaltyHighScorePlayoff)
      )
    );
  }
  if (offsettingPenaltyHighScoreQual.length > 0) {
    highScoresData.push(
      scoreUtils.BuildHighScoreJson(
        +req.params.year,
        'offsetting',
        'qual',
        scoreUtils.FindFTCHighestScore(offsettingPenaltyHighScoreQual)
      )
    );
  }
  if (TOAPenaltyFreeHighScoreQual.length > 0) {
    highScoresData.push(
      scoreUtils.BuildHighScoreJson(
        +req.params.year,
        'TOAPenaltyFree',
        'qual',
        scoreUtils.FindFTCHighestScore(TOAPenaltyFreeHighScoreQual)
      )
    );
  }
  if (TOAPenaltyFreeHighScorePlayoff.length > 0) {
    highScoresData.push(
      scoreUtils.BuildHighScoreJson(
        +req.params.year,
        'TOAPenaltyFree',
        'playoff',
        scoreUtils.FindFTCHighestScore(TOAPenaltyFreeHighScorePlayoff)
      )
    );
  }
  res.json(highScoresData);
});

router.get('/:year/highscores', async (req, res) => {
  res.setHeader('Cache-Control', 's-maxage=600');
  const scores = await GetFTCHighScores(req.params.year);
  res.json(scores);
});

const ensureUser = (req: express.Request, res: express.Response) => {
  if (!(req.auth?.payload['https://gatool.org/roles'] as string[]).includes('user')) {
    res.status(403).send();
  }
};