import { Component, inject } from '@angular/core';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-toast',
  standalone: true,
  styles: [`
    .tm-toast-wrap {
      position: fixed;
      top: 20px;
      right: 20px;
      z-index: 9999;
      display: flex;
      flex-direction: column;
      gap: 8px;
      font-family: 'Plus Jakarta Sans', system-ui, sans-serif;
    }
    .tm-toast {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 11px 14px;
      border-radius: 10px;
      font-size: 13.5px;
      font-weight: 600;
      color: #ffffff;
      box-shadow: 0 4px 20px rgba(0,0,0,.22);
      min-width: 240px;
      max-width: 360px;
    }
    .tm-toast.success { background: #1a9d5a; }
    .tm-toast.error   { background: #e5484d; }
    .tm-toast.info    { background: var(--accent, #5b54e8); }
    .tm-dismiss {
      margin-left: auto;
      background: none;
      border: none;
      color: rgba(255,255,255,.7);
      cursor: pointer;
      font-size: 14px;
      padding: 0 2px;
      line-height: 1;
      flex-shrink: 0;
    }
    .tm-dismiss:hover { color: #fff; }
  `],
  template: `
    <div class="tm-toast-wrap">
      @for (toast of toastSvc.toasts(); track toast.id) {
        <div class="tm-toast {{ toast.type }}">
          <span>{{ toast.message }}</span>
          <button class="tm-dismiss" (click)="toastSvc.dismiss(toast.id)">✕</button>
        </div>
      }
    </div>
  `,
})
export class ToastComponent {
  readonly toastSvc = inject(ToastService);
}
