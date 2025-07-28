/* eslint-disable @typescript-eslint/no-unused-vars */
interface FTCEvent {
  eventId: string;
  address: string;
  remote: boolean;
  hybrid: boolean;
  website: string;
  webcasts: string[] | null;
  timezone: string;
  code: string;
  regionCode: string;
  name: string;
  type: string;
  typeName: string;
  districtCode: string;
  divisionCode: string | null;
  leagueCode: string | null;
  venue: string;
  city: string;
  stateprov: string;
  country: string;
  dateStart: string; // ISO 8601 date format
  dateEnd: string; // ISO 8601 date format
  liveStreamURL: string;
  fieldCount: number;
  published: boolean;
  coordinates: string | null;
}

interface FTCEventListResponse {
  events: FTCEvent[];
  eventCount: number;
}

interface FTCLeague {
  region: string;
  code: string;
  name: string;
  remote: boolean;
  parentLeagueCode: string | null;
  parentLeagueName: string | null;
  location: string;
}

interface FTCLeagueListResponse {
  leagues: FTCLeague[];
  leagueCount: number;
}

interface FTCEventMatch {
  event: {
    eventCode: string;
    regionCode: string | null;
    leagueCode: string | null;
    type: string;
  };
  match: FTCHybridMatch;
}