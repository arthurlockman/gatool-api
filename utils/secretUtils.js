import { SecretClient } from '@azure/keyvault-secrets'
import { useIdentityPlugin, DefaultAzureCredential } from '@azure/identity'

// this is a hack to make this plugin not crash the app in Azure
const vsCodePlugin = process.env.ENABLE_VSCODE_DEBUG && await import("@azure/identity-vscode")
if (process.env.ENABLE_VSCODE_DEBUG === 'true') {
    useIdentityPlugin(vsCodePlugin.vsCodePlugin);
}

var frcApiToken = undefined
var tbaApiToken = undefined

const credential = new DefaultAzureCredential()
const keyVaultName = "GAToolApiKeys"
const url = `https://${keyVaultName}.vault.azure.net`
const azureKeyVaultClient = new SecretClient(url, credential)

export const GetFRCApiToken = async () => {
    if (frcApiToken) {
        return frcApiToken
    }
    frcApiToken = await ReadSecret("FRCApiKey")
    return frcApiToken
}

export const GetTBAApiToken = async () => {
    if (tbaApiToken) {
        return tbaApiToken
    }
    tbaApiToken = await ReadSecret("TBAApiKey")
    return tbaApiToken
}

export const ReadSecret = async (secretName) => {
    return (await azureKeyVaultClient.getSecret(secretName)).value
}