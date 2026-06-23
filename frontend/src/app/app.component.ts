import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { AuthService } from './core/services/auth.service';
import { ToastComponent } from './shared/components/toast/toast.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, ToastComponent],
  template: `
    <div class="min-h-screen bg-base-200">
      <nav class="navbar bg-base-100 shadow-sm px-4 sticky top-0 z-40">
        <div class="flex-1">
          <a routerLink="/tasks" class="btn btn-ghost text-xl font-bold">
            ✅ Task Manager
          </a>
        </div>
        @if (auth.isAuthenticated()) {
          <div class="flex-none items-center gap-3">
            <span class="text-sm opacity-60 hidden sm:inline">
              {{ auth.currentUser()?.email }}
            </span>
            <button class="btn btn-ghost btn-sm" (click)="logout()">Logout</button>
          </div>
        }
      </nav>

      <main class="container mx-auto p-4 max-w-4xl">
        <router-outlet />
      </main>

      <app-toast />
    </div>
  `,
})
export class AppComponent {
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
