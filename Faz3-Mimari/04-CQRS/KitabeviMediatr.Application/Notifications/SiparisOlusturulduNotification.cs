using MediatR;

namespace KitabeviMediatr.Application.Notifications;

public record SiparisOlusturulduNotification(string EventData) : INotification;
//                                                                 ↑ IRequest değil INotification
//                                                                   fark: IRequest → tek handler, tek cevap
//                                                                         INotification → birden fazla handler, cevap yok
//                                                                   bunu yazmasaydık → Publish() derlenmezdi
//
// EventData format: "SiparisOnaylandi:42:kullanici@mail.com"
// Domain entity'deki _domainEvents.Add(...) ile üretilen string
