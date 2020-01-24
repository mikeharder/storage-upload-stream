const net = require('net');

const port = 443;

const STORAGE_CONNECTION_STRING = process.env.STORAGE_CONNECTION_STRING;

if (!STORAGE_CONNECTION_STRING) {
    throw new Error("Environment variable STORAGE_CONNECTION_STRING is not set");
}

let accountName = STORAGE_CONNECTION_STRING.match(/AccountName=([^;]*)/)[1];
let endpointSuffix = STORAGE_CONNECTION_STRING.match(/EndpointSuffix=([^;]*)/)[1];

let host = `${accountName}.blob.${endpointSuffix}`;

log(`Connecting to ${host}:${port}...`);

// let client = new net.Socket();
// client.connect

function log(message) {
    const timeString = new Date().toISOString().substring(11, 23);
    console.log(`[${timeString}] ${message}`);
}

