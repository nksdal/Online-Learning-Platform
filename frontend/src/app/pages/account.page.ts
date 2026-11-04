import { CommonModule } from '@angular/common';
import { Component, ElementRef, OnDestroy, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { UserProfile } from '../core/models';

function apiMessage(error: any, fallback: string): string {
  return error?.error?.title ?? error?.error?.detail ?? fallback;
}

function getTimeZones(): string[] {
  const intlWithTimeZones = Intl as typeof Intl & {
    supportedValuesOf?: (key: 'timeZone') => string[];
  };
  const supported = intlWithTimeZones.supportedValuesOf?.('timeZone') ?? [];
  const local = Intl.DateTimeFormat().resolvedOptions().timeZone;
  return [...new Set(['UTC', 'Asia/Tbilisi', local, ...supported].filter(Boolean))].sort();
}

@Component({
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './account.page.html',
  styleUrl: './account.page.scss',
})
export class AccountPage implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);

  @ViewChild('pictureInput') private pictureInput?: ElementRef<HTMLInputElement>;

  readonly profile = signal<UserProfile | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly uploading = signal(false);
  readonly removingPicture = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly selectedPicture = signal<File | null>(null);
  readonly photoPreview = signal<string | null>(null);
  readonly avatarUrl = computed(() => this.photoPreview() ?? this.profile()?.profileImageUrl ?? null);
  readonly initials = computed(() => {
    const profile = this.profile();
    const first = profile?.firstName?.trim().charAt(0) ?? '';
    const last = profile?.lastName?.trim().charAt(0) ?? '';
    return `${first}${last}`.toUpperCase() || 'N';
  });
  readonly timeZones = getTimeZones();
  readonly form = this.fb.nonNullable.group({
    firstName: ['', [Validators.required, Validators.pattern(/\\S/), Validators.maxLength(100)]],
    lastName: ['', [Validators.required, Validators.pattern(/\\S/), Validators.maxLength(100)]],
    timeZoneId: ['UTC', [Validators.required, Validators.maxLength(64)]],
  });

  async ngOnInit(): Promise<void> {
    try {
      const profile = await firstValueFrom(this.api.profile());
      this.applyProfile(profile);
    } catch (error) {
      this.error.set(apiMessage(error, 'We could not load your account. Please try again.'));
    } finally {
      this.loading.set(false);
    }
  }

  ngOnDestroy(): void {
    this.revokePreview();
  }

  async saveProfile(): Promise<void> {
    this.error.set('');
    this.notice.set('');
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    try {
      const { firstName, lastName, timeZoneId } = this.form.getRawValue();
      const profile = await firstValueFrom(this.api.updateProfile(firstName.trim(), lastName.trim(), timeZoneId));
      this.applyProfile(profile);
      this.notice.set('Your profile details have been saved.');
    } catch (error) {
      this.error.set(apiMessage(error, 'We could not save your profile. Please try again.'));
    } finally {
      this.saving.set(false);
    }
  }

  choosePicture(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.item(0) ?? null;
    if (!file) return;

    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type.toLowerCase())) {
      this.error.set('Choose a JPEG, PNG, or WebP image.');
      input.value = '';
      return;
    }
    if (file.size > 5 * 1024 * 1024) {
      this.error.set('Choose an image that is 5 MB or smaller.');
      input.value = '';
      return;
    }

    this.error.set('');
    this.notice.set('');
    this.revokePreview();
    this.selectedPicture.set(file);
    const preview = URL.createObjectURL(file);
    this.photoPreview.set(preview);
  }

  async uploadPicture(): Promise<void> {
    const file = this.selectedPicture();
    if (!file) return;

    this.error.set('');
    this.notice.set('');
    this.uploading.set(true);
    try {
      const profile = await firstValueFrom(this.api.uploadProfilePicture(file));
      this.applyProfile(profile);
      this.selectedPicture.set(null);
      this.revokePreview();
      if (this.pictureInput) this.pictureInput.nativeElement.value = '';
      this.notice.set('Your profile picture has been updated.');
    } catch (error) {
      this.error.set(apiMessage(error, 'We could not upload that image. Please try again.'));
    } finally {
      this.uploading.set(false);
    }
  }

  async removePicture(): Promise<void> {
    if (!this.profile()?.profileImageUrl) return;

    this.error.set('');
    this.notice.set('');
    this.removingPicture.set(true);
    try {
      const profile = await firstValueFrom(this.api.removeProfilePicture());
      this.applyProfile(profile);
      this.selectedPicture.set(null);
      this.revokePreview();
      if (this.pictureInput) this.pictureInput.nativeElement.value = '';
      this.notice.set('Your profile picture has been removed.');
    } catch (error) {
      this.error.set(apiMessage(error, 'We could not remove your profile picture. Please try again.'));
    } finally {
      this.removingPicture.set(false);
    }
  }

  private applyProfile(profile: UserProfile): void {
    this.profile.set(profile);
    this.auth.setProfile(profile);
    this.form.patchValue({
      firstName: profile.firstName,
      lastName: profile.lastName,
      timeZoneId: profile.timeZoneId,
    });
  }

  private revokePreview(): void {
    const preview = this.photoPreview();
    if (preview) URL.revokeObjectURL(preview);
    this.photoPreview.set(null);
  }
}
