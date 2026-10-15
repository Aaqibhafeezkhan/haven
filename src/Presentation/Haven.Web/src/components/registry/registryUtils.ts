import type { PortMappingDto, ServiceRegistryEntryDto, TlsMode } from '@/api/types';

export function formatUptime(startedAt: string): string {
  const seconds = Math.max(0, Math.floor((Date.now() - new Date(startedAt).getTime()) / 1000));
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ${seconds % 60}s`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ${minutes % 60}m`;
  const days = Math.floor(hours / 24);
  return `${days}d ${hours % 24}h`;
}

export function portHost(ipAddress?: string): string {
  if (!ipAddress || ipAddress === '0.0.0.0' || ipAddress === '::') return 'localhost';
  return ipAddress;
}

export function domainUrl(domain: { hostname: string; tlsMode: TlsMode }): string {
  return `${domain.tlsMode === 'None' ? 'http' : 'https'}://${domain.hostname}`;
}

export interface AddressLink {
  key: string;
  label: string;
  href: string;
  target?: number;
}

export function publishedAddress(port: PortMappingDto): AddressLink {
  const host = portHost(port.ipAddress);
  return {
    key: `host-${port.hostPort}-${port.containerPort}`,
    label: `${host}:${port.hostPort}`,
    href: `http://${host}:${port.hostPort}`,
    target: port.containerPort,
  };
}

export function buildAddresses(entry: ServiceRegistryEntryDto): AddressLink[] {
  const published = entry.ports.filter(p => p.hostPort).map(publishedAddress);
  const internal = entry.ipAddress
    ? entry.ports.map((p, i) => ({
        key: `ip-${i}`,
        label: `${entry.ipAddress}:${p.containerPort}`,
        href: `http://${entry.ipAddress}:${p.containerPort}`,
      }))
    : [];
  return [...published, ...internal];
}

/** "Project › Environment" for services, "Sidecar" for sidecars. Undefined when nothing is known. */
export function entryContext(entry: {
  projectName?: string;
  environmentName?: string;
  sidecarName?: string;
}): string | undefined {
  if (entry.projectName) {
    return entry.environmentName
      ? `${entry.projectName} › ${entry.environmentName}`
      : entry.projectName;
  }
  return undefined;
}

export function canOpenService(serviceId?: string): serviceId is string {
  return !!serviceId && serviceId !== '00000000-0000-0000-0000-000000000000';
}
