import 'dotenv/config';

// noinspection ES6UnusedImports
import newrelic from 'newrelic';
import logger from '../logger';
import { UpdateHighScores } from '../utils/update-high-scores';

logger.info('Updating high scores...');
await UpdateHighScores();
logger.info('Done updating high scores.');
