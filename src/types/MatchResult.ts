type Station = 'Red1' | 'Red2' | 'Red3' | 'Blue1' | 'Blue2' | 'Blue3';

interface Team {
  teamNumber: number;
  station: Station;
  dq: boolean;
}

interface Match {
  actualStartTime: string;
  tournamentLevel: string;
  postResultTime: string;
  description: string;
  matchNumber: number;
  scoreRedFinal: number;
  scoreRedFoul: number;
  scoreRedAuto: number;
  scoreBlueFinal: number;
  scoreBlueFoul: number;
  scoreBlueAuto: number;
  teams: Team[];
}

interface MatchResponse {
  Matches: Match[];
  matches?: Match[];
}
