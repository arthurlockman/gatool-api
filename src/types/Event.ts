/* eslint-disable @typescript-eslint/no-unused-vars */
interface Event {
  address: string;
  website: string;
  webcasts: string[];
  timezone: string;
  code: string;
  divisionCode: string | null;
  name: string;
  type: string;
  districtCode: string;
  venue: string;
  city: string;
  stateprov: string;
  country: string;
  dateStart: string; // ISO 8601 date format
  dateEnd: string; // ISO 8601 date format
}

interface EventListResponse {
  Events: Event[];
  eventCount: number;
}

interface District {
  code: string;
  name: string
}
interface DistrictListResponse {
  districts: District[]
}

interface EventMatch {
  event: {
    eventCode: string;
    districtCode: string;
    type: string;
  };
  match: HybridMatch;
}
