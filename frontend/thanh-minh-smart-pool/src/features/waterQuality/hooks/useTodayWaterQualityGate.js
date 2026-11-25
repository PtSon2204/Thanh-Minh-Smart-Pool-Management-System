import { useEffect } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { hasValidAccessToken, useAuthStore } from '../../auth/store/authStore';
import { hasSessionRole } from '../../auth/utils/sessionRoles';
import { onWaterQualityUpdated, startSignalRConnection } from '../../../services/signalrClient';
import { waterQualityService } from '../services/waterQualityService';

export const TODAY_WATER_QUALITY_GATE_QUERY_KEY = 'water-quality-today-gate';

export function useTodayWaterQualityGate() {
  const session = useAuthStore((state) => state.session);
  const queryClient = useQueryClient();
  const isLifeguard = hasSessionRole(session, 'LIFEGUARD');
  const hasToken = hasValidAccessToken(session);
  const enabled = Boolean(hasToken && isLifeguard);
  const sessionIdentity = session?.id ?? session?.userId ?? session?.email ?? 'anonymous';

  useEffect(() => {
    if (!enabled) return undefined;
    startSignalRConnection();
    const unsub = onWaterQualityUpdated(() => {
      void queryClient.invalidateQueries({
        queryKey: [TODAY_WATER_QUALITY_GATE_QUERY_KEY],
      });
    });
    return () => unsub();
  }, [enabled, queryClient]);

  const query = useQuery({
    queryKey: [TODAY_WATER_QUALITY_GATE_QUERY_KEY, sessionIdentity, isLifeguard],
    queryFn: () => waterQualityService.getTodayDutyGate(),
    enabled,
    staleTime: 5000,
    retry: false,
    retryOnMount: false,
    refetchOnWindowFocus: true,
    refetchOnReconnect: true,
  });

  return {
    ...query,
    isLifeguard,
    mustMeasureWater: Boolean(enabled && query.data?.requiresMeasurement),
    isCheckingGate: Boolean(enabled && query.isLoading && !query.isError && query.errorUpdateCount === 0),
  };
}
