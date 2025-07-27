/* eslint-disable @typescript-eslint/no-unused-vars */
interface TOAEvent {
  event_key: string;
  season_key: string;
  region_key: string | null;
  league_key: string | null;
  event_code: string;
  first_event_code: string;
  event_type_key: string;
  event_region_number: number;
  division_key: number;
  division_name: string | null;
  event_name: string;
  start_date: string;
  end_date: string;
  week_key: string;
  city: string;
  state_prov: string;
  country: string;
  venue: string;
  website: string | null;
  time_zone: string;
  is_public: boolean;
  active_tournament_level: string;
  alliance_count: number;
  field_count: number;
  advance_spots: number;
  advance_event: boolean | null;
  data_source: number;
  team_count: number;
  match_count: number;
}

interface League {
  league_key: string;
  region_key: string;
  season_key: string;
  league_division: string;
  league_description: string;
}

interface Region {
  region_key: string;
  description: string;
}

interface Webcast {
  type: string;
  channel: string;
  date: string;
  file: string;
}

type TOAEventResponse = TOAEvent[];
