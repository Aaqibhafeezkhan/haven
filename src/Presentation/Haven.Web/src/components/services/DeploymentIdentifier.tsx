import { GitCommit, Package } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { DeploymentDto } from '@/api/types';
import { CodeSpan } from '@/components/ui/CodeSpan';
import styles from '@/styles/components/services/DeploymentIdentifier.module.css';

type IdentifierSource = Pick<DeploymentDto, 'identifierType' | 'identifier' | 'commitMessage'>;

/** Short form of an identifier: 7 chars of a commit, or the first 12 hex chars of a `sha256:` digest/ID. */
export function shortIdentifier({ identifierType, identifier }: IdentifierSource): string {
  if (!identifier) return '';
  if (identifierType === 'Commit') return identifier.slice(0, 7);
  const [algorithm, hash] = identifier.includes(':') ? identifier.split(':') : ['', identifier];
  return `${algorithm ? `${algorithm}:` : ''}${hash.slice(0, 12)}`;
}

interface DeploymentIdentifierProps {
  deployment: IdentifierSource;
  /**
   * `compact` is for list rows (short, not copyable); `inline` is a short copyable chip for meta
   * rows, with the full value and commit message in the tooltip.
   */
  variant?: 'compact' | 'inline';
}

export function DeploymentIdentifier({
  deployment,
  variant = 'compact',
}: DeploymentIdentifierProps) {
  const { t } = useTranslation('services');

  if (!deployment.identifier) {
    return <span className={styles.none}>{t('deploymentsTab.noIdentifier')}</span>;
  }

  const isCommit = deployment.identifierType === 'Commit';
  const icon = isCommit ? <GitCommit size={12} /> : <Package size={12} />;

  if (variant === 'compact') {
    return (
      <span className={styles.compact} title={deployment.identifier}>
        <CodeSpan icon={icon}>{shortIdentifier(deployment)}</CodeSpan>
        {isCommit && deployment.commitMessage && (
          <span className={styles.message}>{deployment.commitMessage}</span>
        )}
      </span>
    );
  }

  const identifier = deployment.identifier;
  const message = isCommit ? deployment.commitMessage : undefined;

  return (
    <span className={styles.compact} title={message ? `${identifier}\n${message}` : identifier}>
      <CodeSpan icon={icon} copyable onBeforeCopy={async () => identifier}>
        {shortIdentifier(deployment)}
      </CodeSpan>
      {message && <span className={styles.message}>{message}</span>}
    </span>
  );
}
