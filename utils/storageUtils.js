import { ReadSecret } from "./secretUtils.js"
import { BlobServiceClient } from '@azure/storage-blob'

const blobStorageConnectionString = await ReadSecret('UserStorageConnectionString')
const blobServiceClient = BlobServiceClient.fromConnectionString(
    blobStorageConnectionString
)

const userPrefsContainer = blobServiceClient.getContainerClient('gatool-user-preferences')

/**
 * Get stored user preferences.
 * @param userName The username to retrieve.
 */
export const GetUserPreferences = async (userName) => {
    var userBlob = userPrefsContainer.getBlockBlobClient(`${userName}.prefs.json`);
    var content = await userBlob.download(0);
    return await streamToString(content.readableStreamBody)
}

/**
 * Store a user's preferences.
 * @param userName The username
 * @param preferences The preferences to store.
 */
export const StoreUserPreferences = async (userName, preferences) => {
    var userBlob = userPrefsContainer.getBlockBlobClient(`${userName}.prefs.json`);
    await userBlob.upload(preferences, preferences.length)
}

const streamToString = (stream) => {
    const chunks = [];
    return new Promise((resolve, reject) => {
        stream.on('data', (chunk) => chunks.push(Buffer.from(chunk)))
        stream.on('error', (err) => reject(err))
        stream.on('end', () => resolve(Buffer.concat(chunks).toString('utf8')))
    })
}