interface DistrictRankingsResponse {
  districtRanks: DistrictRank[];
  rankingCountTotal: number;
  rankingCountPage: number;
  pageCurrent: number;
  pageTotal: number;
}

interface DistrictRank {
  districtCode: string;
  teamNumber: number;
  rank: number;
  totalPoints: number;
  event1Code: string | null;
  event1Points: number | null;
  event2Code: string | null;
  event2Points: number | null;
  districtCmpCode: string | null;
  districtCmpPoints: number | null;
  teamAgePoints: number;
  adjustmentPoints: number;
  qualifiedDistrictCmp: boolean;
  qualifiedFirstCmp: boolean;
}
