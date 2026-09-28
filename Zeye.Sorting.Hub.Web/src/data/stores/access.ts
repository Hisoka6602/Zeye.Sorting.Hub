import { initialRoles } from '../mock/access';
import { createStoredList } from './createStoredList';

export const useRoles = createStoredList('zeye-demo-roles', initialRoles);
