import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { Stack } from '@/components/layout';
import { parseEnvFile } from '@/lib/envFile';
import styles from '@/styles/components/services/CommandArgsEditor.module.css';

import { FormGroup, FormLabel } from '../ui/Form';

interface BuildArgsEditorProps {
  buildArgs: Record<string, string>;
  onChange: (args: Record<string, string>) => void;
  disabled?: boolean;
}

interface ArgRow {
  id: number;
  key: string;
  value: string;
}

let nextRowId = 0;

const toRows = (args: Record<string, string>): ArgRow[] =>
  Object.entries(args).map(([key, value]) => ({ id: nextRowId++, key, value }));

const toRecord = (rows: ArgRow[]): Record<string, string> =>
  Object.fromEntries(rows.filter(r => r.key !== '').map(r => [r.key, r.value]));

const sameArgs = (a: Record<string, string>, b: Record<string, string>) => {
  const aKeys = Object.keys(a);
  return aKeys.length === Object.keys(b).length && aKeys.every(k => a[k] === b[k]);
};

export function BuildArgsEditor({ buildArgs, onChange, disabled }: BuildArgsEditorProps) {
  const { t } = useTranslation('services');
  const [pasteOpen, setPasteOpen] = useState(false);
  const [pasteText, setPasteText] = useState('');
  const [rows, setRows] = useState<ArgRow[]>(() => toRows(buildArgs));

  // Rows are the source of truth while editing so duplicate or blank keys survive in the UI;
  // only resync when the parent changes the args to something the rows don't already represent.
  const [syncedArgs, setSyncedArgs] = useState(buildArgs);
  if (syncedArgs !== buildArgs) {
    setSyncedArgs(buildArgs);
    if (!sameArgs(toRecord(rows), buildArgs)) setRows(toRows(buildArgs));
  }

  const parsedArgs = parseEnvFile(pasteText);
  const parsedCount = Object.keys(parsedArgs).length;
  const hasBlankRow = rows.some(r => r.key === '');

  const commit = (next: ArgRow[]) => {
    setRows(next);
    onChange(toRecord(next));
  };

  const importArgs = (mode: 'merge' | 'replace') => {
    if (parsedCount === 0) return;
    commit(toRows(mode === 'merge' ? { ...toRecord(rows), ...parsedArgs } : parsedArgs));
    setPasteText('');
    setPasteOpen(false);
  };

  const updateRow = (id: number, patch: Partial<ArgRow>) => {
    commit(rows.map(r => (r.id === id ? { ...r, ...patch } : r)));
  };

  const removeArg = (id: number) => {
    commit(rows.filter(r => r.id !== id));
  };

  const addArg = () => {
    if (hasBlankRow) return;
    commit([...rows, { id: nextRowId++, key: '', value: '' }]);
  };

  return (
    <FormGroup>
      <div className={styles.labelWithHelp}>
        <FormLabel>{t('createPage.buildArgs')}</FormLabel>
        <span className={styles.helpText}>{t('createPage.buildArgsHelp')}</span>
      </div>
      <Stack gap="3" className={styles.argsContainer}>
        {rows.length === 0 ? (
          <p className={styles.emptyState}>{t('createPage.noBuildArgs')}</p>
        ) : (
          rows.map(({ id, key, value }) => (
            <div key={id} className={styles.argRow}>
              <input
                type="text"
                className={styles.argInput}
                placeholder={t('createPage.buildArgKeyPlaceholder')}
                value={key}
                onChange={e => updateRow(id, { key: e.target.value })}
                disabled={disabled}
              />
              <input
                type="text"
                className={styles.argInput}
                placeholder={t('createPage.buildArgValuePlaceholder')}
                value={value}
                onChange={e => updateRow(id, { value: e.target.value })}
                disabled={disabled}
              />
              <button
                type="button"
                className={styles.argRemove}
                onClick={() => removeArg(id)}
                disabled={disabled}
              >
                ×
              </button>
            </div>
          ))
        )}
      </Stack>
      <div className={styles.actions}>
        <button
          type="button"
          className={styles.addArgButton}
          onClick={addArg}
          disabled={disabled || hasBlankRow}
        >
          {t('createPage.addBuildArg')}
        </button>
        <button
          type="button"
          className={styles.addArgButton}
          onClick={() => setPasteOpen(o => !o)}
          disabled={disabled}
        >
          {t('createPage.fromEnvFile')}
        </button>
      </div>
      {pasteOpen && (
        <div className={styles.pastePanel}>
          <textarea
            className={styles.pasteInput}
            placeholder={t('createPage.fromEnvFilePlaceholder')}
            value={pasteText}
            onChange={e => setPasteText(e.target.value)}
            disabled={disabled}
            rows={4}
            autoFocus
          />
          <span className={styles.helpText}>
            {t('createPage.fromEnvFilePreview', { count: parsedCount })}
          </span>
          <div className={styles.actions}>
            <button
              type="button"
              className={styles.addArgButton}
              onClick={() => importArgs('merge')}
              disabled={disabled || parsedCount === 0}
            >
              {t('createPage.fromEnvFileMerge')}
            </button>
            <button
              type="button"
              className={styles.addArgButton}
              onClick={() => importArgs('replace')}
              disabled={disabled || parsedCount === 0}
            >
              {t('createPage.fromEnvFileReplace')}
            </button>
          </div>
        </div>
      )}
    </FormGroup>
  );
}
