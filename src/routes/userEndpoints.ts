import express from 'express';
import {
  GetUserPreferences,
  StoreUserPreferences
} from '../utils/storageUtils';
import logger from '../logger';

export const router = express.Router();

// User Data Storage

router.get('/preferences', async (req, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  try {
    const email = req.auth?.payload.email as string;
    const prefs = await GetUserPreferences(email);
    res.json(JSON.parse(prefs));
  } catch (e) {
    logger.error(e);
    res.status(404).send();
  }
});

router.put('/preferences', async (req, res) => {
  const email = req.auth?.payload.email as string;
  await StoreUserPreferences(email, req.body);
  res.status(204).send();
});