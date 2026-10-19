import { clsx } from 'clsx';
import { Check, Copy } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';

import styles from '@/styles/components/ui/CopyButton.module.css';

interface CopyButtonProps {
  value: string;
  label: string;
  className?: string;
}

async function writeToClipboard(value: string): Promise<boolean> {
  try {
    await navigator.clipboard.writeText(value);
    return true;
  } catch {
    // Clipboard API is unavailable on insecure origins (e.g. plain-http LAN access).
    try {
      const area = document.createElement('textarea');
      area.value = value;
      area.style.position = 'fixed';
      area.style.opacity = '0';
      document.body.appendChild(area);
      area.select();
      const ok = document.execCommand('copy');
      document.body.removeChild(area);
      return ok;
    } catch {
      return false;
    }
  }
}

/** Small icon button that copies `value` to the clipboard and briefly confirms with a check mark. */
export function CopyButton({ value, label, className }: CopyButtonProps) {
  const [copied, setCopied] = useState(false);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(
    () => () => {
      if (timer.current) clearTimeout(timer.current);
    },
    []
  );

  const handleClick = async (e: React.MouseEvent) => {
    e.stopPropagation();
    e.preventDefault();
    if (!(await writeToClipboard(value))) return;
    setCopied(true);
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => setCopied(false), 1500);
  };

  return (
    <button
      type="button"
      className={clsx(styles.button, copied && styles.copied, className)}
      onClick={handleClick}
      title={label}
      aria-label={label}
    >
      {copied ? <Check size={12} /> : <Copy size={12} />}
    </button>
  );
}
