/** @type {import('tailwindcss').Config} */
import animate from "tailwindcss-animate"
import plugin from "tailwindcss/plugin"

export default {
    darkMode: ["class"],
    content: [
        './pages/**/*.{ts,tsx,vue}',
        './components/**/*.{ts,tsx,vue}',
        './app/**/*.{ts,tsx,vue}',
        './src/**/*.{ts,tsx,vue}',
    ],
    theme: {
        container: {
            center: true,
            padding: "2rem",
            screens: {
                "2xl": "1400px",
            },
        },
        extend: {
            colors: {
                // Custom Project Colors
                'bg-dark': 'var(--bg-dark)',
                'bg-panel': 'var(--bg-panel)',
                'neon-cyan': 'var(--neon-cyan)',
                'neon-blue': 'var(--neon-blue)',
                'beam-pink': 'var(--beam-pink)',
                'gundam-gold': 'var(--gundam-gold)',
                'bonus-orange': 'var(--bonus-orange)',
                'machine-silver': 'var(--machine-silver)',
                'text-white': '#ffffff',
                'text-dim': 'var(--text-dim)',

                // Shadcn Defaults (Mapped to placeholders or generic)
                border: "hsl(var(--border))",
                input: "hsl(var(--input))",
                ring: "hsl(var(--ring))",
                background: "hsl(var(--background))",
                foreground: "hsl(var(--foreground))",
                primary: {
                    DEFAULT: "hsl(var(--primary))",
                    foreground: "hsl(var(--primary-foreground))",
                },
                secondary: {
                    DEFAULT: "hsl(var(--secondary))",
                    foreground: "hsl(var(--secondary-foreground))",
                },
                destructive: {
                    DEFAULT: "hsl(var(--destructive))",
                    foreground: "hsl(var(--destructive-foreground))",
                },
                muted: {
                    DEFAULT: "hsl(var(--muted))",
                    foreground: "hsl(var(--muted-foreground))",
                },
                accent: {
                    DEFAULT: "hsl(var(--accent))",
                    foreground: "hsl(var(--accent-foreground))",
                },
                popover: {
                    DEFAULT: "hsl(var(--popover))",
                    foreground: "hsl(var(--popover-foreground))",
                },
                card: {
                    DEFAULT: "hsl(var(--card))",
                    foreground: "hsl(var(--card-foreground))",
                },
            },
            borderRadius: {
                lg: "var(--radius)",
                md: "calc(var(--radius) - 2px)",
                sm: "calc(var(--radius) - 4px)",
            },
            fontFamily: {
                rajdhani: ['Rajdhani', 'sans-serif'],
                roboto: ['Roboto', 'sans-serif'],
                mono: ['"Roboto Mono"', 'monospace'],
            },
            keyframes: {
                "accordion-down": {
                    from: { height: 0 },
                    to: { height: "var(--radix-accordion-content-height)" },
                },
                "accordion-up": {
                    from: { height: "var(--radix-accordion-content-height)" },
                    to: { height: 0 },
                },
            },
            animation: {
                "accordion-down": "accordion-down 0.2s ease-out",
                "accordion-up": "accordion-up 0.2s ease-out",
            },
        },
    },
    plugins: [
        animate,
        plugin(function({ addUtilities }) {
            addUtilities({
                '.bg-neon-cyan-5': { 'background-color': 'color-mix(in srgb, var(--neon-cyan), transparent 95%)' },
                '.bg-neon-cyan-10': { 'background-color': 'color-mix(in srgb, var(--neon-cyan), transparent 90%)' },
                '.bg-neon-cyan-20': { 'background-color': 'color-mix(in srgb, var(--neon-cyan), transparent 80%)' },
                '.bg-neon-cyan-30': { 'background-color': 'color-mix(in srgb, var(--neon-cyan), transparent 70%)' },
                '.border-neon-cyan-30': { 'border-color': 'color-mix(in srgb, var(--neon-cyan), transparent 70%)' },
                '.border-neon-cyan-40': { 'border-color': 'color-mix(in srgb, var(--neon-cyan), transparent 60%)' },
                '.border-neon-cyan-50': { 'border-color': 'color-mix(in srgb, var(--neon-cyan), transparent 50%)' },
                '.border-neon-cyan-60': { 'border-color': 'color-mix(in srgb, var(--neon-cyan), transparent 40%)' },
                '.bg-neon-blue-5': { 'background-color': 'color-mix(in srgb, var(--neon-blue), transparent 95%)' },
                '.bg-neon-blue-10': { 'background-color': 'color-mix(in srgb, var(--neon-blue), transparent 90%)' },
                '.border-neon-blue-40': { 'border-color': 'color-mix(in srgb, var(--neon-blue), transparent 60%)' },
                '.border-neon-blue-50': { 'border-color': 'color-mix(in srgb, var(--neon-blue), transparent 50%)' },
                '.bg-beam-pink-5': { 'background-color': 'color-mix(in srgb, var(--beam-pink), transparent 95%)' },
            })
        })
    ],
}
