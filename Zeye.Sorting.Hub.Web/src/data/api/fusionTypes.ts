export interface FusionSettings {
  isEnabled: boolean; allowInsecureHttp: boolean; discoveryEnabled: boolean; discoveryPort: number; advertisedEndpoint: string;
  maxBatchRecords: number; maxBatchBytes: number; maxImageChunkBytes: number; maxImageBytes: number;
  maxPendingImagesPerSource: number; leaseSeconds: number; uploadRetentionHours: number;
}
export interface FusionSource {
  sourceInstanceId: string; workstationName: string; enabled: boolean; tenantId: string; storagePartitionId: string;
  lineId: string; siteCode: string | null; deviceCode: string | null; timeZoneId: string; identityLocked?: boolean;
}
export interface FusionConfiguration { revision: number; settings: FusionSettings; hubId: string; imageDirectory: string; sources: FusionSource[] }
export interface FusionPresence { sourceInstanceId: string; isOnline: boolean; lastSeenAt: string | null; pendingFacts: number; pendingImages: number; rejectedFacts: number }
export interface FusionPairing {
  format: 'zeye.fusion-hub.pairing'; protocolVersion: '1.0'; hubId: string; sourceInstanceId: string; machineApiKey: string;
  lineId: string; timeZoneId: string; siteCode: string; deviceCode: string; endpoint: string; allowInsecureHttp: boolean; discoveryPort: number;
}
export interface FusionPairingResult { revision: number; pairing: FusionPairing }
