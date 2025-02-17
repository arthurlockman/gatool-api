import { SecretClient } from '@azure/keyvault-secrets';
import { DefaultAzureCredential } from '@azure/identity';

let frcApiToken: string;
let tbaApiToken: string;

const credential = new DefaultAzureCredential();
const keyVaultName = 'GAToolApiKeys';
const url = `https://${keyVaultName}.vault.azure.net`;
const azureKeyVaultClient = new SecretClient(url, credential);

export const GetFRCApiToken = async () => {
  if (frcApiToken) {
    return frcApiToken;
  }
  frcApiToken = await ReadSecret('FRCApiKey');
  return frcApiToken;
};

export const GetTBAApiToken = async () => {
  if (tbaApiToken) {
    return tbaApiToken;
  }
  tbaApiToken = await ReadSecret('TBAApiKey');
  return tbaApiToken;
};

export const GetAuth0AdminTokens = async () => {
  const client_id = await ReadSecret('Auth0AdminClientId');
  const client_secret = await ReadSecret('Auth0AdminClientSecret');
  return {
    client_id,
    client_secret
  };
};

export const ReadSecret = async (secretName: string) => {
  return (await azureKeyVaultClient.getSecret(secretName))?.value ?? '';
};
