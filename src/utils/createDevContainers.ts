import { BlobServiceClient } from '@azure/storage-blob';

const blobStorageConnectionString = "UseDevelopmentStorage=true"
const blobServiceClient = BlobServiceClient.fromConnectionString(blobStorageConnectionString);

blobServiceClient.createContainer('gatool-user-preferences');
blobServiceClient.createContainer('gatool-team-updates');
blobServiceClient.createContainer('gatool-team-updates-history');
blobServiceClient.createContainer('gatool-high-scores');
