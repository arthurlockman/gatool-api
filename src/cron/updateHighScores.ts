import 'dotenv/config';

// noinspection ES6UnusedImports
import newrelic from 'newrelic';
import logger from '../logger';
import { UpdateHighScores } from '../utils/update-high-scores';

await newrelic.startBackgroundTransaction('highScoresUpdater', async () => {
  logger.info('Updating high scores...');
  await UpdateHighScores();
  logger.info('Done updating high scores.');
})

newrelic.shutdown({ collectPendingData: true }, () => process.exit(0))
