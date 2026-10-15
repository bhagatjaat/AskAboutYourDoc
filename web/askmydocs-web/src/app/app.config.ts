import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withFetch } from '@angular/common/http';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),

    // withFetch() - HttpClient ko XHR ke bajay fetch par chalata hai.
    // Modern, aur SSR me bhi theek kaam karta hai.
    provideHttpClient(withFetch())
  ]
};
