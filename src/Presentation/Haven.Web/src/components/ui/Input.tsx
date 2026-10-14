import { clsx } from 'clsx';
import { Info } from 'lucide-react';
import { InputHTMLAttributes } from 'react';

import styles from '@/styles/components/ui/Input.module.css';

import { Tooltip } from './Tooltip';

interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string;
  /** Short explanation shown in a tooltip next to the label. */
  hint?: string;
}

export function Input({ label, error, hint, required, className, ...props }: InputProps) {
  return (
    <div className={styles.wrapper}>
      {label && (
        <label className={styles.label} htmlFor={props.id}>
          {label}
          {required && <span className={styles.required}>*</span>}
          {hint && (
            <Tooltip content={hint} direction="above" wrap>
              <Info size={13} className={styles.hintIcon} aria-label={hint} />
            </Tooltip>
          )}
        </label>
      )}
      <input required={required} className={clsx(styles.input, error && styles.inputError, className)} {...props} />
      {error && <p className={styles.errorMessage}>{error}</p>}
    </div>
  );
}
