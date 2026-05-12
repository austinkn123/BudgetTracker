import axios from 'axios';
import { fetchAuthSession, signOut } from 'aws-amplify/auth';

const api = axios.create({
  baseURL: '/api',
});

api.interceptors.request.use(async (config) => {
  try {
    const session = await fetchAuthSession();
    const idToken = session.tokens?.idToken?.toString();
    if (idToken) {
      config.headers.Authorization = `Bearer ${idToken}`;
    }
  } catch (error) {
    console.error('Failed to get auth token:', error);
  }
  return config;
});

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    if (error.response?.status === 401) {
      await signOut();
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

export default api;
