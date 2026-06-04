import type { ReactNode } from 'react';

interface ModalProps {
  open: boolean;
  onClose: () => void;
  title?: string;
  children: ReactNode;
  footer?: ReactNode;
  size?: 'sm' | 'md' | 'lg';
}

export function Modal({ open, onClose, title, children, footer, size = 'md' }: ModalProps) {
  if (!open) return null;
  const sizeCls = { sm: 'max-w-sm', md: 'max-w-xl', lg: 'max-w-3xl' }[size];
  return (
    <div className="fixed inset-0 bg-black/40 z-40 flex items-center justify-center p-4" onClick={onClose}>
      <div
        className={`bg-white rounded-lg shadow-xl w-full ${sizeCls} max-h-[90vh] flex flex-col`}
        onClick={(e) => e.stopPropagation()}
      >
        {title && (
          <div className="px-5 py-3 border-b border-slate-200 flex items-center justify-between">
            <h3 className="text-base font-semibold">{title}</h3>
            <button onClick={onClose} className="text-slate-400 hover:text-slate-600 text-xl">
              ×
            </button>
          </div>
        )}
        <div className="p-5 overflow-y-auto flex-1">{children}</div>
        {footer && <div className="px-5 py-3 border-t border-slate-200 flex justify-end gap-2">{footer}</div>}
      </div>
    </div>
  );
}
