/* eslint-disable @typescript-eslint/no-unused-vars */
interface FTCTeamResponse {
  teams: FTCTeam[];
  teamCountTotal: number;
  teamCountPage: number;
  pageCurrent: number;
  pageTotal: number;
}

interface FTCTeam {
  teamNumber: number;
  displayTeamNumber: string;
  nameFull: string;
  nameShort: string;
  schoolName: string;
  city: string;
  stateProv: string;
  country: string;
  website: string;
  rookieYear: number;
  robotName: string;
  districtCode: null | string;
  homeCMP: string;
  homeRegion: string;
  displayLocation: string;
}
