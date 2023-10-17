import { ExportResult, ExportResultCode } from '@opentelemetry/core';
import { ReadableSpan, SpanExporter } from '@opentelemetry/sdk-trace-base';

export class ConsoleTraceLinkExporter implements SpanExporter {
    export(spans: ReadableSpan[], resultCallback: (result: ExportResult) => void): void {

        spans.filter(span => span.parentSpanId == undefined)
             .forEach(span => {
            console.log(`http://localhost:16686/trace/${span.spanContext().traceId}`);
        });

        return resultCallback({ code: ExportResultCode.SUCCESS });
    }
    shutdown(): Promise<void> {
        throw new Error('Method not implemented.');
    }
    forceFlush?(): Promise<void> {
        throw new Error('Method not implemented.');
    }

}