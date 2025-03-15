import 'dotenv/config';

// noinspection ES6UnusedImports
import newrelic from 'newrelic';
import logger from '../logger';
import { SyncUsers } from '../utils/syncUsers';

logger.info('Starting user sync...');
await SyncUsers();
logger.info('User sync complete.');
