/* eslint-disable @typescript-eslint/no-unused-vars */
interface TOATeam {
  team_key: string;
  region_key: string;
  league_key: string;
  team_number: number;
  team_name_short: string;
  team_name_long: string;
  robot_name: string;
  last_active: number;
  city: string;
  state_prov: string;
  zip_code: number;
  country: string;
  rookie_year: number;
  website: string;
}

type TOATeamResponse = TOATeam[];
