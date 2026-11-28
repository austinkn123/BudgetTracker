import { isAdminToken } from './adminAccess';
import { useAuth } from './useAuth';

/** Whether the signed-in user is in the Cognito "admin" group (UI only; the server re-checks). */
export const useIsAdmin = (): boolean => isAdminToken(useAuth().idToken);
