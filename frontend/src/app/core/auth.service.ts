import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiService } from './api.service';
import { UserProfile } from './models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiService);
  private readonly storedToken = sessionStorage.getItem('olp_access_token');
  readonly token = signal<string | null>(this.storedToken);
  readonly user = signal<UserProfile | null>(null);
  readonly isAuthenticated = computed(() => !!this.token());

  async initialize(): Promise<void> {
    try {
      const response = await firstValueFrom(this.api.refresh());
      this.setToken(response.accessToken);
      await this.loadProfile();
    } catch {
      this.clear();
    }
  }

  async signIn(email: string, password: string): Promise<void> {
    const response = await firstValueFrom(this.api.login(email, password));
    this.setToken(response.accessToken);
    await this.loadProfile();
  }

  async loadProfile(): Promise<void> {
    this.user.set(await firstValueFrom(this.api.profile()));
  }

  async signOut(): Promise<void> {
    try { await firstValueFrom(this.api.logout()); } finally { this.clear(); }
  }

  updateToken(token: string): void { this.setToken(token); }

  private setToken(token: string): void {
    sessionStorage.setItem('olp_access_token', token);
    this.token.set(token);
  }

  private clear(): void {
    sessionStorage.removeItem('olp_access_token');
    this.token.set(null);
    this.user.set(null);
  }
}
