using 'main.bicep'

param name = 'voice-ai'
param location = 'swedencentral'
param currentUserObjectId = readEnvironmentVariable('AZURE_USER_OBJECT_ID', '')
