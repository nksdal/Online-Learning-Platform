import { Component, effect, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  readonly auth = inject(AuthService);
  readonly menuOpen = signal(false);
  readonly year = new Date().getFullYear();
  private readonly router = inject(Router);
  constructor() {
    effect(() => {
      if (this.auth.mustChangePassword() && this.router.url !== '/change-password') {
        void this.router.navigateByUrl('/change-password');
      }
    });
  }
  async signOut(): Promise<void> {
    this.menuOpen.set(false);
    await this.auth.signOut();
    await this.router.navigateByUrl('/');
  }

  closeMenu(): void { this.menuOpen.set(false); }
}
