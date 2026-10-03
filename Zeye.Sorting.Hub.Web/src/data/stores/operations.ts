import { normalizeExceptionRules } from '../exceptionRules';
import { initialExceptionRules, initialLiveEvents, initialRules } from '../mock/operations';
import { createStoredList } from './createStoredList';

export const useLiveEvents = createStoredList('zeye-demo-events', initialLiveEvents);
export const useRules = createStoredList('zeye-demo-rules', initialRules);
export const useExceptionRules = createStoredList('zeye-demo-exception-rules', initialExceptionRules, normalizeExceptionRules);
