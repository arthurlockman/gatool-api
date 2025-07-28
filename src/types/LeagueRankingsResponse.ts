/* eslint-disable @typescript-eslint/no-unused-vars */
interface LeagueRankingsResponse {
  rankings: LeagueRank[];
}

interface LeagueRank {
  rank: number;
  teamNumber: number;
  displayTeamNumber: string;
  teamName: string;
  sortOrder1: number;
  sortOrder2: number;
  sortOrder3: number;
  sortOrder4: number;
  sortOrder5: number;
  sortOrder6: number;
  totalPoints: number;
  wins: number;
  losses: number;
  ties: number;
  qualAverage: number;
  dq: number;
  matchesPlayed: number;
  matchesCounted: number;
}
