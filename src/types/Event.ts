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

interface EventMatch {
  event: {
    eventCode: string;
    type: string;
  };
  match: HybridMatch;
}
