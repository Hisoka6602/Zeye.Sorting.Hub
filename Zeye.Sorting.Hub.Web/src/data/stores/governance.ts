import { initialArchiveTasks } from '../mock/governance';
import { createStoredList } from './createStoredList';

export const useArchiveTasks = createStoredList('zeye-demo-archive', initialArchiveTasks);
