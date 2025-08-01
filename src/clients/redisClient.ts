import * as redis from 'redis';
import logger from '../logger';

export const REDIS_RETENTION_1_DAY = 86400;
export const REDIS_RETENTION_3_DAY = REDIS_RETENTION_1_DAY * 3;
export const REDIS_RETENTION_7_DAY = REDIS_RETENTION_1_DAY * 7;
export const REDIS_RETENTION_14_DAY = REDIS_RETENTION_1_DAY * 14;
export const REDIS_RETENTION_12_HOUR = REDIS_RETENTION_1_DAY / 2;

const redisDisabled = process.env.DISABLE_REDIS === 'true';

let redisClient = null;
if (!redisDisabled) {
  const redisHost = process.env.REDIS_HOST || 'gatool-redis-01';
  const redisPort = process.env.REDIS_PORT || '10000';
  const redisPassword = process.env.REDIS_PASSWORD;
  const redisTls = process.env.REDIS_TLS === 'true';

  logger.info(`Connecting to redis on ${redisHost}:${redisPort}`);
  const redisUrl = redisTls
    ? `rediss://:${redisPassword}@${redisHost}:${redisPort}`
    : `redis://${redisHost}:${redisPort}`;

  redisClient = redis.createClient({
    url: redisUrl,
    socket: {
      tls: redisTls,
      rejectUnauthorized: true
    }
  });
  redisClient.on('error', (error) => logger.error(`Error : ${error}`));
  await redisClient.connect();
  logger.info(`Connected to redis.`);
} else {
  logger.warn('Redis disabled by CLI argument.');
}

export const getRedisItem = async (key: string) => {
  return redisDisabled ? null : await redisClient?.get(key);
};

export const setRedisItem = async (key: string, value: string, expiration: number) => {
  if (!redisDisabled) {
    await redisClient?.set(key, value, {
      EX: expiration
    });
  }
};
