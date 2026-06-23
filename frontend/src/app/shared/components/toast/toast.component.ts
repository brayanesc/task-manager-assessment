import { Component, inject } from '@angular/core';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-toast',
  standalone: true,
  template: `
    <div class="toast toast-top toast-end z-50">
      @for (toast of toastSvc.toasts(); track toast.id) {
        <div
          class="alert shadow-lg"
          [class.alert-error]="toast.type === 'error'"
          [class.alert-success]="toast.type === 'success'"
          [class.alert-info]="toast.type === 'info'"
        >
          <span>{{ toast.message }}</span>
          <button
            class="btn btn-ghost btn-xs"
            (click)="toastSvc.dismiss(toast.id)"
          >✕</button>
        </div>
      }
    </div>
  `,
})
export class ToastComponent {
  readonly toastSvc = inject(ToastService);
}
