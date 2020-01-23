const { randomBytes } = require("crypto");
const { PassThrough} = require("stream");
const { BlobServiceClient } = require("@azure/storage-blob");

const iterations = 1;
const uploadSize = 9 * 1024 * 1024;
const bufferSize = 4 * 1024 * 1024;
const maxConcurrency = 1;

async function main() {
    requestLogger(require('http'));
    requestLogger(require('https'));

    const STORAGE_CONNECTION_STRING = process.env.STORAGE_CONNECTION_STRING;

    if (!STORAGE_CONNECTION_STRING) {
        throw new Error("Environment variable STORAGE_CONNECTION_STRING is not set");
    }

    const blobServiceClient = BlobServiceClient.fromConnectionString(STORAGE_CONNECTION_STRING);

    const containerName = `container${new Date().getTime()}`;
    const containerClient = blobServiceClient.getContainerClient(containerName);

    log(`Creating container ${containerName}`);
    await containerClient.create();
    log(`Created container ${containerName}`);

    const randomBuffer = randomBytes(uploadSize);

    for (let i=0; i < iterations; i++) {
        log(`Iteration ${i}`);

        const blobName = `blob${new Date().getTime()}`;
        const blockBlobClient = containerClient.getBlockBlobClient(blobName);
        
        const rs = new PassThrough();
        rs.end(randomBuffer);
        
        log(`Uploading blob ${blobName}`);
        await blockBlobClient.uploadStream(rs, bufferSize, maxConcurrency);
        log(`Uploaded blob ${blobName}`);
    }

    log(`Deleting container ${containerName}`);
    await containerClient.delete();
    log(`Deleted container ${containerName}`);
}

function requestLogger(httpModule){
    var original = httpModule.request
    httpModule.request = function(options, callback) {
        log(`${options.method} ${options.href}`);
        return original(options, callback)
    }
}

function log(message) {
    const timeString = new Date().toISOString().substring(11, 23);
    console.log(`[${timeString}] ${message}`);
}

main().catch((err) => {
    console.error("Error running sample: ", err.message);
});