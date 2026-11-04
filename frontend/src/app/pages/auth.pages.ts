import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';

function apiMessage(error: any, fallback: string): string {
  return error?.error?.title ?? error?.error?.detail ?? fallback;
}

@Component({
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  template: `
    <main class="auth-wrap"><div class="auth-aside"><span class="eyebrow">A PLACE TO GROW</span><h1>Make time<br>for <em>yourself.</em></h1><p>One lesson at a time, you’re building something that belongs to you.</p><div class="aside-mark">✳</div></div>
      <section class="auth-panel"><a class="back-link" routerLink="/">← Back to courses</a><span class="eyebrow">WELCOME BACK</span><h2>Sign in</h2><p class="auth-intro">Pick up where your curiosity left off.</p>
        @if (notice()) { <div class="form-notice" role="status">{{ notice() }}</div> }
        @if (error()) { <div class="form-error" role="alert">{{ error() }}</div> }
        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <label>Email address<input type="email" formControlName="email" autocomplete="email" placeholder="you@example.com"></label>
          <label>Password<input type="password" formControlName="password" autocomplete="current-password" placeholder="Your password"></label>
          <button class="button button-dark full-button" [disabled]="busy()">{{ busy() ? 'Signing in…' : 'Sign in' }} <span>↗</span></button>
        </form>
        <div class="resend-verification">
          <span>Didn't receive the confirmation email?</span>
          <button type="button" class="text-button" [disabled]="resendBusy() || form.controls.email.invalid" (click)="resendVerification()">
            {{ resendBusy() ? 'Sending…' : 'Resend verification email' }}
          </button>
          @if (resendMessage()) { <p role="status">{{ resendMessage() }}</p> }
        </div>
        <div class="auth-links"><a routerLink="/forgot-password">Forgot password?</a><span>New here? <a routerLink="/register">Create an account</a></span></div>
        <div class="auth-foot">SECURE SIGN-IN · YOUR LEARNING IS YOURS</div>
      </section>
    </main>
  `,
})
export class LoginPage {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  readonly busy = signal(false); readonly error = signal(''); readonly notice = signal('');
  readonly resendBusy = signal(false); readonly resendMessage = signal('');
  readonly form = this.fb.nonNullable.group({ email: ['', [Validators.required, Validators.email]], password: ['', Validators.required] });

  constructor() {
    if (this.route.snapshot.queryParamMap.get('verified') === '1') this.notice.set('Verification request submitted. Try signing in with your account.');
    if (this.route.snapshot.queryParamMap.get('registered') === '1') this.notice.set('Account created. Check your inbox and spam folder for the verification email.');
  }

  async submit(): Promise<void> {
    this.error.set(''); this.busy.set(true);
    try {
      const { email, password } = this.form.getRawValue();
      await this.auth.signIn(email.trim(), password);
      const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
      const roles = this.auth.user()?.roles ?? [];
      const destination = this.auth.mustChangePassword()
        ? '/change-password'
        : returnUrl?.startsWith('/') ? returnUrl
        : roles.includes('Admin') ? '/admin'
        : roles.includes('Teacher') ? '/teacher'
        : roles.includes('Student') ? '/my-learning' : '/';
      await this.router.navigateByUrl(destination);
    } catch (err) { this.error.set(apiMessage(err, 'Sign-in failed. Check your email, password, and verification status.')); }
    finally { this.busy.set(false); }
  }

  async resendVerification(): Promise<void> {
    const email = this.form.controls.email.value.trim();
    if (this.form.controls.email.invalid || !email) return;
    this.resendBusy.set(true);
    this.resendMessage.set('');
    try {
      await firstValueFrom(this.api.resendVerification(email));
      this.resendMessage.set('If this account needs verification, a new email will arrive shortly. Check your inbox and spam folder.');
    } catch {
      this.resendMessage.set('We could not submit the request just now. Please wait a moment and try again.');
    } finally {
      this.resendBusy.set(false);
    }
  }
}

@Component({
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <main class="auth-wrap"><div class="auth-aside"><span class="eyebrow">ONE LAST STEP</span><h1>Secure your<br><em>admin account.</em></h1><p>Choose a personal password before continuing to the administrator workspace.</p><div class="aside-mark">✳</div></div>
      <section class="auth-panel"><span class="eyebrow">PASSWORD UPDATE REQUIRED</span><h2>Set your password</h2><p class="auth-intro">Your temporary password has been used for the first sign-in only.</p>
        @if (error()) { <div class="form-error" role="alert">{{ error() }}</div> }
        @if (notice()) { <div class="form-notice" role="status">{{ notice() }}</div> }
        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <label>Current password<input type="password" formControlName="currentPassword" autocomplete="current-password"></label>
          <label>New password<input type="password" formControlName="newPassword" autocomplete="new-password" placeholder="At least 10 characters"><small>Use 10+ characters with uppercase, lowercase, a number, and at least 4 different characters.</small></label>
          <label>Confirm new password<input type="password" formControlName="confirmPassword" autocomplete="new-password"></label>
          @if (form.controls.confirmPassword.touched && !passwordsMatch()) { <small class="password-match-error">The new passwords do not match.</small> }
          @if (form.controls.newPassword.touched && !hasEnoughDistinctCharacters()) { <small class="password-match-error">Use at least four different characters.</small> }
          <button class="button button-dark full-button" [disabled]="busy() || form.invalid || !passwordsMatch() || !hasEnoughDistinctCharacters()">{{ busy() ? 'Updating password…' : 'Update password' }} <span>↗</span></button>
        </form>
        <button class="sign-in-link change-password-signout" type="button" (click)="signOut()">Sign out</button>
        <div class="auth-foot">YOUR ADMIN ACCOUNT · PROTECTED BY A PERSONAL PASSWORD</div>
      </section>
    </main>
  `,
})
export class ChangePasswordPage {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly form = this.fb.nonNullable.group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(256), Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$/)]],
    confirmPassword: ['', Validators.required],
  });

  passwordsMatch(): boolean {
    const value = this.form.getRawValue();
    return value.newPassword === value.confirmPassword;
  }

  hasEnoughDistinctCharacters(): boolean { return new Set(this.form.controls.newPassword.value).size >= 4; }

  async submit(): Promise<void> {
    this.error.set(''); this.notice.set(''); this.busy.set(true);
    try {
      const value = this.form.getRawValue();
      const result = await firstValueFrom(this.api.changePassword(value.currentPassword, value.newPassword));
      this.auth.updateToken(result.accessToken);
      await this.auth.loadProfile();
      const roles = this.auth.user()?.roles ?? [];
      await this.router.navigateByUrl(roles.includes('Admin') ? '/admin' : roles.includes('Teacher') ? '/teacher' : '/my-learning');
    } catch (err) {
      this.error.set(apiMessage(err, 'The password could not be updated. Check your current password and new-password requirements.'));
    } finally { this.busy.set(false); }
  }

  async signOut(): Promise<void> {
    await this.auth.signOut();
    await this.router.navigateByUrl('/login');
  }
}

@Component({
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  template: `
    <main class="auth-wrap"><div class="auth-aside"><span class="eyebrow">START WHERE YOU ARE</span><h1>Your next<br>chapter <em>starts here.</em></h1><p>Join a learning community built around steady progress and practical skills.</p><div class="aside-mark">✳</div></div>
      <section class="auth-panel"><a class="back-link" routerLink="/">← Back to courses</a><span class="eyebrow">CREATE YOUR ACCOUNT</span><h2>Join us</h2><p class="auth-intro">A few details, then you’re on your way.</p>
        @if (sent()) {
          <div class="form-notice" role="status"><strong>Your account is ready for verification.</strong><br>Check your inbox and spam folder for the confirmation link.</div>
          <div class="resend-verification">
            <span>Didn't receive it?</span>
            <button type="button" class="text-button" [disabled]="resendBusy()" (click)="resendVerification()">
              {{ resendBusy() ? 'Sending…' : 'Resend verification email' }}
            </button>
            @if (resendMessage()) { <p role="status">{{ resendMessage() }}</p> }
          </div>
        }
        @if (error()) { <div class="form-error" role="alert">{{ error() }}</div> }
        @if (!sent()) {
          <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
            <div class="form-pair"><label>First name<input formControlName="firstName" autocomplete="given-name" placeholder="First name"></label><label>Last name<input formControlName="lastName" autocomplete="family-name" placeholder="Last name"></label></div>
            <label>Email address<input type="email" formControlName="email" autocomplete="email" placeholder="you@example.com"></label>
            <label>Password<input type="password" formControlName="password" autocomplete="new-password" placeholder="At least 10 characters"><small>Use 10+ characters with uppercase, lowercase, and a number.</small></label>
            <button class="button button-dark full-button" [disabled]="busy()">{{ busy() ? 'Creating account…' : 'Create account' }} <span>↗</span></button>
          </form>
        }
        <div class="auth-links"><span>Already a member? <a routerLink="/login">Sign in</a></span></div><div class="auth-foot">YOUR ACCOUNT · YOUR PACE · YOUR PROGRESS</div>
      </section>
    </main>
  `,
})
export class RegisterPage {
  private readonly fb = inject(FormBuilder); private readonly api = inject(ApiService);
  readonly busy = signal(false); readonly error = signal(''); readonly sent = signal(false);
  readonly resendBusy = signal(false); readonly resendMessage = signal('');
  readonly form = this.fb.nonNullable.group({
    firstName: ['', [Validators.required, Validators.maxLength(100)]], lastName: ['', [Validators.required, Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email]], password: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(256)]],
  });
  async submit(): Promise<void> {
    this.error.set(''); this.busy.set(true);
    try {
      const value = this.form.getRawValue();
      await firstValueFrom(this.api.register({ ...value, email: value.email.trim(), timeZoneId: Intl.DateTimeFormat().resolvedOptions().timeZone }));
      this.sent.set(true);
    } catch (err) { this.error.set(apiMessage(err, 'We couldn’t create this account. Check the details and try again.')); }
    finally { this.busy.set(false); }
  }

  async resendVerification(): Promise<void> {
    const email = this.form.controls.email.value.trim();
    if (!email) return;
    this.resendBusy.set(true);
    this.resendMessage.set('');
    try {
      await firstValueFrom(this.api.resendVerification(email));
      this.resendMessage.set('If this account needs verification, a new email will arrive shortly. Check your inbox and spam folder.');
    } catch {
      this.resendMessage.set('We could not submit the request just now. Please wait a moment and try again.');
    } finally {
      this.resendBusy.set(false);
    }
  }
}

@Component({
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  template: `
    <main class="auth-wrap"><div class="auth-aside"><span class="eyebrow">A FRESH START</span><h1>Get back<br>to <em>learning.</em></h1><p>We’ll send a secure reset link to the email on your account.</p><div class="aside-mark">↗</div></div>
      <section class="auth-panel"><a class="back-link" routerLink="/login">← Back to sign in</a><span class="eyebrow">PASSWORD HELP</span><h2>Reset password</h2><p class="auth-intro">Enter the address you used to join.</p>
        @if (sent()) { <div class="form-notice" role="status">If the request can be completed, a reset link will be sent to that email. Check the inbox and spam folder.</div> }
        @if (error()) { <div class="form-error" role="alert">{{ error() }}</div> }
        @if (!sent()) { <form [formGroup]="form" (ngSubmit)="submit()"><label>Email address<input type="email" formControlName="email" autocomplete="email" placeholder="you@example.com"></label><button class="button button-dark full-button" [disabled]="busy()">{{ busy() ? 'Sending…' : 'Send reset link' }} <span>↗</span></button></form> }
        <div class="auth-foot">WE’LL ONLY SEND A LINK IF THE ACCOUNT IS ELIGIBLE</div>
      </section>
    </main>
  `,
})
export class ForgotPasswordPage {
  private readonly fb = inject(FormBuilder); private readonly api = inject(ApiService);
  readonly busy = signal(false); readonly error = signal(''); readonly sent = signal(false);
  readonly form = this.fb.nonNullable.group({ email: ['', [Validators.required, Validators.email]] });
  async submit(): Promise<void> {
    this.error.set(''); this.busy.set(true);
    try { await firstValueFrom(this.api.forgotPassword(this.form.getRawValue().email.trim())); this.sent.set(true); }
    catch (err) { this.error.set(apiMessage(err, 'We couldn’t submit the request. Try again shortly.')); }
    finally { this.busy.set(false); }
  }
}

@Component({
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  template: `
    <main class="auth-wrap"><div class="auth-aside"><span class="eyebrow">YOU’RE ALMOST THERE</span><h1>Choose a<br><em>new password.</em></h1><p>Make it strong, memorable, and yours.</p><div class="aside-mark">✳</div></div>
      <section class="auth-panel"><a class="back-link" routerLink="/login">← Back to sign in</a><span class="eyebrow">ACCOUNT RECOVERY</span><h2>Set a new password</h2><p class="auth-intro">Choose a password you haven’t used here before.</p>
        @if (complete()) { <div class="form-notice" role="status">Password changed. <a routerLink="/login">Sign in with your new password.</a></div> }
        @if (error()) { <div class="form-error" role="alert">{{ error() }}</div> }
        @if (!complete()) { <form [formGroup]="form" (ngSubmit)="submit()"><label>New password<input type="password" formControlName="password" autocomplete="new-password" placeholder="At least 10 characters"><small>Use uppercase, lowercase, a number, and 4 different characters.</small></label><button class="button button-dark full-button" [disabled]="busy() || !token || !userId">{{ busy() ? 'Updating…' : 'Update password' }} <span>↗</span></button></form> }
        @if (!userId || !token) { <div class="form-error">This reset link is incomplete. Request a new one.</div><a routerLink="/forgot-password" class="text-button">Request another link</a> }
        <div class="auth-foot">PASSWORD RESET LINK · SECURE AND TIME-LIMITED</div>
      </section>
    </main>
  `,
})
export class ResetPasswordPage implements OnInit {
  private readonly fb = inject(FormBuilder); private readonly api = inject(ApiService); private readonly route = inject(ActivatedRoute); private readonly router = inject(Router);
  readonly busy = signal(false); readonly error = signal(''); readonly complete = signal(false); readonly form = this.fb.nonNullable.group({ password: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(256)]] });
  userId = ''; token = '';
  ngOnInit(): void { this.userId = this.route.snapshot.queryParamMap.get('userId') ?? ''; this.token = this.route.snapshot.queryParamMap.get('token') ?? ''; }
  async submit(): Promise<void> {
    this.error.set(''); this.busy.set(true);
    try { await firstValueFrom(this.api.resetPassword(this.userId, this.token, this.form.getRawValue().password)); this.complete.set(true); }
    catch (err) { this.error.set(apiMessage(err, 'This reset link may have expired. Request a new one and try again.')); }
    finally { this.busy.set(false); }
  }
}

@Component({
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <main class="verify-wrap"><div class="verify-symbol">{{ busy() ? '…' : success() ? '✓' : '!' }}</div><span class="eyebrow">EMAIL CONFIRMATION</span><h1>{{ busy() ? 'Confirming your address…' : success() ? 'Request submitted.' : 'We couldn’t submit that link.' }}</h1>
      <p>{{ message() }}</p>
      @if (success()) { <a class="button button-dark" [routerLink]="['/login']" [queryParams]="{ verified: 1 }">Continue to sign in <span>↗</span></a> }
      @else if (!busy()) { <a class="button button-dark" routerLink="/register">Create an account <span>↗</span></a> }
    </main>
  `,
})
export class VerifyEmailPage implements OnInit {
  private readonly api = inject(ApiService); private readonly route = inject(ActivatedRoute);
  readonly busy = signal(true); readonly success = signal(false); readonly message = signal('');
  async ngOnInit(): Promise<void> {
    const userId = this.route.snapshot.queryParamMap.get('userId') ?? '';
    const token = this.route.snapshot.queryParamMap.get('token') ?? '';
    if (!userId || !token) { this.busy.set(false); this.message.set('The verification link is missing information. Request another verification email and try again.'); return; }
    try {
      const result = await firstValueFrom(this.api.verifyEmail(userId, token));
      this.success.set(true); this.message.set(`${result.message} If the link was valid, your address is confirmed. You can try signing in now.`);
    } catch (err) {
      this.message.set(apiMessage(err, 'The link could not be submitted. It may have expired; request another verification email.'));
    } finally { this.busy.set(false); }
  }
}
