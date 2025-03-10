/* eslint-disable @typescript-eslint/no-unused-vars */
interface TeamResponse {
  teams: Team[];
  teamCountTotal: number;
  teamCountPage: number;
  pageCurrent: number;
  pageTotal: number;
}

interface Team {
  schoolName: string;
  website: string;
  homeCMP: string;
  teamNumber: number;
  nameFull: string;
  nameShort: string;
  city: string;
  stateProv: string;
  country: string;
  rookieYear: number;
  robotName: string;
  districtCode: null | string;
}
