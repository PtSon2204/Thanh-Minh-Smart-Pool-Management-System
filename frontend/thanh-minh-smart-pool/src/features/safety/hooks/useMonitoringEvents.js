import { useEffect, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { getSignalRConnection, onSignalRStateChange, startSignalRConnection, signalRIdentity } from '../../../services/signalrClient'
import { useAuthStore } from '../../auth/store/authStore'

export function useMonitoringEvents() {
  const client = useQueryClient()
  const identity = useAuthStore(state => signalRIdentity(state.session))
  const [state, setState] = useState(() => getSignalRConnection().state === 'Connected' ? 'connected' : 'connecting')
  useEffect(() => {
    const connection = getSignalRConnection()
    const update = () => client.invalidateQueries({ queryKey: ['safety'] })
    const events = ['alert_created', 'alert_escalated', 'alert_updated', 'incident_created']
    events.forEach(event => connection.on(event, update))
    const unsubscribe = onSignalRStateChange(next => { setState(next); if (next === 'connected') update() })
    void startSignalRConnection()
    return () => { events.forEach(event => connection.off(event, update)); unsubscribe() }
  }, [client, identity])
  return state
}
