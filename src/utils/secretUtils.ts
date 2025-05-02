import { SecretClient } from '@azure/keyvault-secrets';
import { DefaultAzureCredential } from '@azure/identity';

let frcApiToken: string;
let tbaApiToken: string;

let azureKeyVaultClient: SecretClient;

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

function getKeyVaultClient() {
  if(azureKeyVaultClient) {
    return azureKeyVaultClient;
  }
  const credential = new DefaultAzureCredential();
  const keyVaultName = 'GAToolApiKeys';
  const url = `https://${keyVaultName}.vault.azure.net`;
  azureKeyVaultClient = new SecretClient(url, credential);
  return azureKeyVaultClient;
}

export const ReadSecret = async (secretName: string) => {
  // Allow avoiding attempting to load Azure credentials in local development
  if(`SECRET_${secretName}` in process.env) {
    return process.env[`SECRET_${secretName}`] || '';
  }
  return (await getKeyVaultClient().getSecret(secretName))?.value ?? '';
};
