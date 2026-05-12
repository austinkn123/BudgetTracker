import { Amplify } from 'aws-amplify';

const region = import.meta.env.VITE_COGNITO_REGION;
const userPoolId = import.meta.env.VITE_COGNITO_USER_POOL_ID;
const userPoolClientId = import.meta.env.VITE_COGNITO_USER_POOL_CLIENT_ID;

if (!region || !userPoolId || !userPoolClientId) {
  throw new Error(
    'Missing required Cognito environment variables: VITE_COGNITO_REGION, VITE_COGNITO_USER_POOL_ID, VITE_COGNITO_USER_POOL_CLIENT_ID'
  );
}

export const amplifyConfig = {
  Auth: {
    Cognito: {
      region,
      userPoolId,
      userPoolClientId,
      loginWith: {
        email: true,
      },
    },
  },
};

Amplify.configure(amplifyConfig);

export default amplifyConfig;
