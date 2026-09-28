import { initialParcels } from '../mock/parcels';
import { createStoredList } from './createStoredList';

export const useParcels = createStoredList('zeye-demo-parcels', initialParcels);
