/**
 * Finds the highest score of a list of matches
 * @param matches Matches to find the highest score of
 */
const FindHighestScore = (matches: EventMatch[]) => {
  if (matches.length === 0) {
    return {};
  }

  let highScore = 0;
  let alliance = '';
  let _match;
  for (const match of matches) {
    if (match.match.scoreBlueFinal > highScore) {
      highScore = match.match.scoreBlueFinal;
      alliance = 'blue';
      _match = match;
    }
    if (match.match.scoreRedFinal > highScore) {
      highScore = match.match.scoreRedFinal;
      alliance = 'red';
      _match = match;
    }
  }
  return {
    event: _match?.event,
    highScoreAlliance: alliance,
    match: _match?.match
  };
};

/**
 * Build a JSON object for a high score
 * @param year The year
 * @param type The score type
 * @param level The score level
 * @param match The score match data
 */
const BuildHighScoreJson = (
  year: number,
  type: string,
  level: string,
  match: object | { event: any; highScoreAlliance: string; match: any }
) => {
  return {
    yearType: year + type + level,
    year,
    type,
    level,
    matchData: match
  };
};

export { FindHighestScore, BuildHighScoreJson };
