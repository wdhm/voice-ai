using 'main.bicep'

param name = readEnvironmentVariable('AZURE_ENV_NAME', 'voice-ai')
param location = 'swedencentral'
param currentUserObjectId = readEnvironmentVariable('AZURE_USER_OBJECT_ID', '')
